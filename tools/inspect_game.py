"""Read-only static inspection. Outputs extracted data only under the requested output folder."""
import argparse, collections, hashlib, io, json, math, re, struct, sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'local' / 'python-deps'))
import UnityPy
import lz4.block
import pefile

def dump(path, value):
    path.write_text(json.dumps(value, indent=2, ensure_ascii=True), encoding='utf-8')

def unpack(fmt, f):
    return struct.unpack(fmt, f.read(struct.calcsize(fmt)))

def zstring(f):
    b = bytearray()
    while (c := f.read(1)) != b'\0':
        if not c: raise EOFError('Unterminated string')
        b += c
    return b.decode('utf-8')

def decompress(data, size, flags):
    mode = flags & 63
    if mode == 0: result = data
    elif mode in (2, 3): result = lz4.block.decompress(data, uncompressed_size=size)
    else: raise ValueError(f'Unsupported compression {mode}')
    if len(result) != size: raise ValueError('Decompressed size mismatch')
    return result

def archive_index(f):
    if zstring(f) != 'UnityFS': raise ValueError('Expected UnityFS')
    version, = unpack('>I', f)
    player, engine = zstring(f), zstring(f)
    size, cs, us, flags = unpack('>QIII', f)
    if version < 7: raise ValueError('This reader requires UnityFS 7+')
    f.seek((f.tell()+15)//16*16)
    start = f.tell()
    if flags & 128: f.seek(size-cs)
    r = io.BytesIO(decompress(f.read(cs), us, flags))
    data_start = start if flags & 128 else start + cs
    if flags & 512: data_start = (data_start+15)//16*16
    r.read(16)
    count, = unpack('>I', r)
    blocks = [unpack('>IIH', r) for _ in range(count)]
    count, = unpack('>I', r)
    nodes = []
    for _ in range(count):
        off, length, nf = unpack('>QQI', r)
        nodes.append(dict(offset=off, size=length, flags=nf, name=zstring(r)))
    return dict(format=version, engine=engine, flags=flags, blocks=len(blocks),
                uncompressed_bytes=sum(b[0] for b in blocks), nodes=nodes), blocks, data_start

def extract_node(f, blocks, start, node):
    f.seek(start)
    position = 0
    result = bytearray()
    for us, cs, flags in blocks:
        if position >= node['offset'] + node['size']: break
        if position + us > node['offset']:
            data = decompress(f.read(cs), us, flags)
            lo = max(0, node['offset']-position)
            hi = min(us, node['offset']+node['size']-position)
            result.extend(data[lo:hi])
        else: f.seek(cs, 1)
        position += us
    if len(result) != node['size']: raise ValueError('Extracted node size mismatch')
    return bytes(result)

def inspect(root, out):
    out.mkdir(parents=True, exist_ok=True)
    data = root / 'Project Prospice_Data'
    pe = pefile.PE(str(root / 'GameAssembly.dll'))
    exports = [{'name': (e.name or b'').decode(), 'rva': hex(e.address)} for e in pe.DIRECTORY_ENTRY_EXPORT.symbols]
    dump(out/'native.json', {'machine':hex(pe.FILE_HEADER.Machine), 'exports':exports,
        'imports':[e.dll.decode() for e in pe.DIRECTORY_ENTRY_IMPORT],
        'sections':[{'name':s.Name.decode().strip('\0'), 'bytes':s.SizeOfRawData, 'entropy':s.get_entropy()} for s in pe.sections]})
    metadata = (data/'il2cpp_data/Metadata/global-metadata.dat').read_bytes()
    counts = collections.Counter(metadata)
    dump(out/'metadata.json', {'bytes':len(metadata), 'sha256':hashlib.sha256(metadata).hexdigest(),
        'header':metadata[:32].hex(' '), 'entropy':-sum(n/len(metadata)*math.log2(n/len(metadata)) for n in counts.values())})
    shared, tables, locales = {}, {}, []
    bundles = data/'StreamingAssets/aa/StandaloneWindows64'
    for path in sorted(bundles.glob('*.bundle')):
        env = UnityPy.load(str(path))
        for obj in env.objects:
            if obj.type.name != 'MonoBehaviour': continue
            tree = obj.read_typetree()
            if 'm_TableCollectionName' in tree:
                shared[tree['m_TableCollectionName']] = {str(e['m_Id']): e['m_Key'] for e in tree['m_Entries']}
            elif 'm_TableData' in tree:
                tables[tree['m_Name']] = {str(e['m_Id']): e['m_Localized'] for e in tree['m_TableData']}
            elif 'm_Identifier' in tree:
                locales.append({'path_id':obj.path_id,'data':tree})
    dump(out/'localization-shared.json', shared)
    dump(out/'localization-raw.json', tables)
    dump(out/'locales.json', locales)
    joined = {}
    coverage = []
    for name, values in tables.items():
        group, locale = name.rsplit('_',1)
        keys = shared[group]
        joined[name] = {keys.get(k, 'UNKNOWN:'+k):v for k,v in values.items()}
        coverage.append({'table':name,'shared_keys':len(keys),'entries':len(values),
                         'empty':sum(not v.strip() for v in values.values()),
                         'missing':len(set(keys)-set(values)), 'unknown_ids':len(set(values)-set(keys))})
    dump(out/'localization-joined.json', joined)
    dump(out/'localization-coverage.json', coverage)
    assets, scripts, components = [], [], []
    script_map = {}
    with (data/'data.unity3d').open('rb') as f:
        index, blocks, start = archive_index(f)
        dump(out/'archive-index.json', index)
        for node in index['nodes']:
            if not node['flags'] & 4 or node['size'] > 80*1024*1024: continue
            print('Inspecting',node['name'],node['size'],flush=True)
            payload = extract_node(f,blocks,start,node)
            env = UnityPy.load(payload)
            counts = collections.Counter(o.type.name for o in env.objects)
            record = {'name':node['name'],'objects':len(env.objects),'types':dict(counts),'named':[], 'errors':[]}
            for obj in env.objects:
                kind = obj.type.name
                if kind in ('MonoScript','GameObject','MonoBehaviour','TextAsset','PlayerSettings','BuildSettings','ResourceManager'):
                    try:
                        if kind == 'MonoScript':
                            s = obj.read()
                            scripts.append({'file':node['name'],'path_id':obj.path_id,'class':s.m_ClassName,
                                            'namespace':s.m_Namespace,'assembly':s.m_AssemblyName})
                            if node['name'] == 'globalgamemanagers.assets':
                                script_map[obj.path_id] = scripts[-1]
                        elif kind in ('BuildSettings','PlayerSettings','ResourceManager'):
                            tree = obj.read_typetree()
                            if kind == 'BuildSettings':
                                tree = {k:v for k,v in tree.items() if k in ('scenes','m_Version','m_GraphicsAPIs')}
                            if kind == 'PlayerSettings':
                                tree = {k:v for k,v in tree.items() if k in ('companyName','productName','bundleVersion','runInBackground','usePlayerLog','resizableWindow','fullscreenMode')}
                            dump(out/(node['name'].replace('/','_')+'-'+kind+'.json'), tree)
                        else:
                            name = obj.peek_name()
                            if name: record['named'].append({'type':kind,'id':obj.path_id,'name':name})
                            if kind == 'MonoBehaviour' and node['name'] in ('level0','level2','level3'):
                                basic = obj.read(check_read=False)
                                script = script_map.get(basic.m_Script.path_id)
                                # Resolve only references whose external file is the observed script file.
                                ext = obj.assets_file.externals
                                fid = basic.m_Script.file_id
                                valid = fid > 0 and fid <= len(ext) and ext[fid-1].path.endswith('globalgamemanagers.assets')
                                wanted = {'Bootstrap','SaveManager','DebugLogManager','DebugMaster','ConsoleCommands','SettingsMaster','Engine','Fuel','Gearbox','CarController','PlayerCore','PlayerController','UpgradeManager'}
                                if valid and script and script['class'] in wanted:
                                    component = {'asset':node['name'],'id':obj.path_id,'class':script['class'],'enabled':basic.m_Enabled}
                                    try: component['fields'] = obj.read_typetree()
                                    except Exception as exc: component['field_read_error'] = str(exc)[:160]
                                    go = obj.assets_file.objects.get(basic.m_GameObject.path_id)
                                    if go:
                                        game_object = go.read()
                                        component['game_object'] = game_object.m_Name
                                        component['game_object_active_self'] = game_object.m_IsActive
                                    components.append(component)
                    except Exception as exc:
                        if len(record['errors']) < 8: record['errors'].append({'type':kind,'error':str(exc)[:160]})
            assets.append(record)
    dump(out/'assets.json', assets)
    dump(out/'scripts.json', scripts)
    dump(out/'components.json', components)
    print('Done:',out,flush=True)

if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--game-dir', required=True, type=Path)
    p.add_argument('--output', required=True, type=Path)
    args = p.parse_args()
    root, output = args.game_dir.resolve(), args.output.resolve()
    if not (root/'GameAssembly.dll').is_file(): p.error('GameAssembly.dll missing')
    if output == root or root in output.parents: p.error('Output must be outside game installation')
    inspect(root, output)

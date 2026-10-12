"""Build a private, hash-bound surface cache from installed assets; never redistribute it."""
import argparse, hashlib, struct, sys
from pathlib import Path
from inspect_game import archive_index, extract_node, UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

HASH = '511B68EBDE548CF7BFE4178B5F8F76CF9397B4C8821EE411079DD6225C7D339D'
def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--game-dir',default=r'E:\SteamLibrary\steamapps\common\Prospice Demo')
    args=parser.parse_args(); root=Path(args.game_dir)
    if hashlib.sha256((root/'GameAssembly.dll').read_bytes()).hexdigest().upper()!=HASH: raise SystemExit('Unsupported game build')
    cache=root/'BepInEx/cache'/('prospice-paint-'+HASH[:12]+'.bin')
    entries={}; ambiguous=set()
    with (root/'Project Prospice_Data/data.unity3d').open('rb') as f:
        index,blocks,start=archive_index(f)
        nodes={n['name']:n for n in index['nodes']}
        for name,node in nodes.items():
            if not node['flags']&4:continue
            env=UnityPy.Environment()
            env.load_file(extract_node(f,blocks,start,node),name=name)
            meshes=[o for o in env.objects if o.type.name=='Mesh']
            if not meshes:continue
            # Mesh stream data is stored in the corresponding resS member.
            resource=name+'.resS'
            if resource in nodes:env.load_file(extract_node(f,blocks,start,nodes[resource]),name=resource)
            for obj in meshes:
                mesh=obj.read()
                # Static world batches are never moving cargo or truck surfaces.
                # Static batches are included for building graffiti.
                handler=MeshHandler(mesh);handler.process()
                vertices=handler.m_Vertices; uvs=handler.m_UV0
                if not vertices or len(vertices)>2000000:continue
                groups=list(handler.get_triangles())
                if len(groups)!=len(mesh.m_SubMeshes):continue
                if not uvs or len(uvs)!=len(vertices):uvs=[(0,0)]*len(vertices)
                a=mesh.m_LocalAABB
                bounds=(a.m_Center.x,a.m_Center.y,a.m_Center.z,a.m_Extent.x,a.m_Extent.y,a.m_Extent.z)
                namebytes=mesh.m_Name.encode('utf-8')
                data=bytearray(struct.pack('<i',len(namebytes))+namebytes+struct.pack('<ii6f',len(vertices),len(groups),*bounds))
                data.extend(struct.pack('<'+'f'*(len(vertices)*3),*(x for v in vertices for x in v[:3])))
                data.extend(struct.pack('<'+'f'*(len(vertices)*2),*(x for v in uvs for x in v[:2])))
                counts=[]
                for group in groups:
                    indices=[i for tri in group for i in tri];counts.append(len(indices))
                    if any(i<0 or i>=len(vertices) for i in indices):raise ValueError('Invalid mesh index')
                    data.extend(struct.pack('<i',len(indices)));data.extend(struct.pack('<'+'i'*len(indices),*indices))
                key=(mesh.m_Name,len(vertices),tuple(counts),struct.pack('<6f',*bounds))
                if key in entries and entries[key]!=data:ambiguous.add(key)
                else:entries[key]=data
    for key in ambiguous:entries.pop(key,None)
    cache.parent.mkdir(parents=True,exist_ok=True)
    temporary=cache.with_suffix('.tmp')
    with temporary.open('wb') as out:
        out.write(b'PPAINT01'+HASH.encode('ascii')+struct.pack('<i',len(entries)))
        for data in entries.values():out.write(data)
    temporary.replace(cache)
    print(f'Private paint cache: {len(entries)} meshes, {len(ambiguous)} ambiguous meshes omitted, {cache.stat().st_size} bytes. {cache}')

if __name__=='__main__':main()

"""Read-only scan of this game's process for structurally valid IL2CPP v29/v31 metadata."""
import argparse, ctypes as C, hashlib, json, struct
from ctypes import wintypes as W
from pathlib import Path

class MBI(C.Structure):
    _fields_ = [('BaseAddress', C.c_void_p), ('AllocationBase', C.c_void_p),
                ('AllocationProtect', W.DWORD), ('PartitionId', W.WORD),
                ('RegionSize', C.c_size_t), ('State', W.DWORD),
                ('Protect', W.DWORD), ('Type', W.DWORD)]

k = C.WinDLL('kernel32', use_last_error=True)
k.OpenProcess.argtypes = [W.DWORD, W.BOOL, W.DWORD]; k.OpenProcess.restype = W.HANDLE
k.CloseHandle.argtypes = [W.HANDLE]
k.VirtualQueryEx.argtypes = [W.HANDLE, C.c_void_p, C.POINTER(MBI), C.c_size_t]; k.VirtualQueryEx.restype = C.c_size_t
k.ReadProcessMemory.argtypes = [W.HANDLE,C.c_void_p,C.c_void_p,C.c_size_t,C.POINTER(C.c_size_t)]; k.ReadProcessMemory.restype=W.BOOL
k.QueryFullProcessImageNameW.argtypes=[W.HANDLE,W.DWORD,W.LPWSTR,C.POINTER(W.DWORD)]; k.QueryFullProcessImageNameW.restype=W.BOOL

SECTIONS = ['stringLiteral','stringLiteralData','string','events','properties','methods',
'parameterDefaultValues','fieldDefaultValues','fieldAndParameterDefaultValueData','fieldMarshaledSizes',
'parameters','fields','genericParameters','genericParameterConstraints','genericContainers','nestedTypes',
'interfaces','vtableMethods','interfaceOffsets','typeDefinitions','images','assemblies','fieldRefs',
'referencedAssemblies','attributeData','attributeDataRange','unresolvedVirtualCallParameterTypes',
'unresolvedVirtualCallParameterRanges','windowsRuntimeTypeNames','windowsRuntimeStrings','exportedTypeDefinitions']

def scan(pid, game_dir, output):
    handle = k.OpenProcess(0x410, False, pid) # PROCESS_QUERY_INFORMATION | PROCESS_VM_READ
    if not handle: raise C.WinError(C.get_last_error())
    try:
        path=C.create_unicode_buffer(32768); length=W.DWORD(len(path))
        if not k.QueryFullProcessImageNameW(handle,0,path,C.byref(length)):raise C.WinError(C.get_last_error())
        expected=game_dir/'Project Prospice.exe'
        if Path(path.value).resolve()!=expected:raise ValueError('PID is not the requested game executable')
        def read(address,size):
            buf=C.create_string_buffer(size); got=C.c_size_t()
            k.ReadProcessMemory(handle,C.c_void_p(address),buf,size,C.byref(got))
            return buf.raw[:got.value]
        def validate(address):
            header=read(address,256)
            if len(header)!=256:return None
            magic,version=struct.unpack_from('<II',header)
            if magic!=0xFAB11BAF or version not in (29,31):return None
            pairs=[struct.unpack_from('<II',header,8+i*8) for i in range(31)]
            if any(size and (off<256 or off+size>64*1024*1024) for off,size in pairs):return None
            end=max(off+size for off,size in pairs)
            if end<1024*1024 or pairs[2][1]<1000:return None
            payload=read(address,end)
            if len(payload)!=end:return None
            off,size=pairs[2];strings=payload[off:off+size]
            required=[b'Assembly-CSharp',b'UnityEngine',b'PlayerHeldItem',b'Pickupable']
            if not all(s in strings for s in required):return None
            output.mkdir(parents=True,exist_ok=True)
            dest=output/'global-metadata.runtime.dat'
            if dest.exists():raise FileExistsError(dest)
            dest.write_bytes(payload)
            report={'pid':pid,'address':hex(address),'version':version,'bytes':len(payload),
                    'sha256':hashlib.sha256(payload).hexdigest(),'file':str(dest),
                    'game_assembly_sha256':hashlib.sha256((game_dir/'GameAssembly.dll').read_bytes()).hexdigest(),
                    'protected_metadata_sha256':hashlib.sha256((game_dir/'Project Prospice_Data/il2cpp_data/Metadata/global-metadata.dat').read_bytes()).hexdigest(),
                    'validation':'31 section bounds and required game strings; downstream parser still required',
                    'sections':{name:{'offset':off,'bytes':size} for name,(off,size) in zip(SECTIONS,pairs)}}
            (output/'metadata-recovery.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
            return report
        address=0; scanned=0; hits=0
        signature=bytes.fromhex('af1bb1fa')
        while address<0x7FFFFFFF0000:
            mbi=MBI()
            if not k.VirtualQueryEx(handle,C.c_void_p(address),C.byref(mbi),C.sizeof(mbi)):break
            base=mbi.BaseAddress or 0; size=mbi.RegionSize
            if size==0:break
            if mbi.State==0x1000 and not (mbi.Protect&0x101) and mbi.Type!=0x1000000:
                offset=0; tail=b''
                while offset<size:
                    data=read(base+offset,min(1024*1024,size-offset)); scanned+=len(data)
                    combined=tail+data;pos=0
                    while (pos:=combined.find(signature,pos))>=0:
                        hits+=1; candidate=base+offset-len(tail)+pos
                        result=validate(candidate)
                        if result:
                            print(json.dumps({'status':'candidate_validated','scanned_bytes':scanned,**result},indent=2));return 0
                        pos+=4
                    tail=data[-7:] if data else b''; offset+=1024*1024
            address=base+size
        print(json.dumps({'status':'not_found','scanned_bytes':scanned,'signature_hits':hits}));return 2
    finally:k.CloseHandle(handle)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--pid',required=True,type=int);p.add_argument('--game-dir',required=True,type=Path)
    p.add_argument('--output',required=True,type=Path);args=p.parse_args()
    game=args.game_dir.resolve();out=args.output.resolve()
    if out==game or game in out.parents:p.error('Output must be outside game installation')
    raise SystemExit(scan(args.pid,game,out))

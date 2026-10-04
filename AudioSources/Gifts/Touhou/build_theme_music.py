from pathlib import Path
import argparse,subprocess,json,hashlib,shutil
import numpy as np
ROOT=Path(__file__).resolve().parents[3]
SOURCE=Path(__file__).parent/'Music'
RATE=44100
TRACKS=[('FantasySeal','9 - 少女綺想曲',77.0,24.0,False),('HourOfDaybreak','15 - 竹取飛翔',310.0,None,False),('MasterSpark','10 - 恋色マスタースパーク',90.5,24.0,False),('TheWorld','触手猴 - 月時計',90.5,None,False),('ResurrectionButterfly','上海爱莉丝幻乐团 (上海アリス幻樂団) - 幽雅',164.5,24.0,False)]

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--ffmpeg',required=True);ap.add_argument('--only',nargs='*');args=ap.parse_args();SOURCE.mkdir(exist_ok=True)
 candidates=[*Path('C:/Users/jinshengkai/Documents/录音').glob('*.m4a'),*Path('C:/Users/jinshengkai/Music').glob('*.ogg'),*Path('C:/Users/jinshengkai/Music').glob('*.opus')]
 reports=json.loads((SOURCE/'edits.json').read_text(encoding='utf-8')) if args.only and (SOURCE/'edits.json').exists() else []
 for name,prefix,rough,duration,recording in TRACKS:
  if args.only and name not in args.only:continue
  reports=[r for r in reports if r['character']!=name]
  original=next(p for p in candidates if p.name.startswith(prefix))
  archive=SOURCE/(name+original.suffix)
  if not archive.exists():shutil.copy2(original,archive)
  # Find the nearby strongest onset rather than cutting inside a sustained note.
  raw=subprocess.run([args.ffmpeg,'-v','error','-i',str(archive),'-ss',str(max(0,rough-.45)),'-t','1.0','-ac','1','-ar','12000','-f','f32le','pipe:1'],capture_output=True,check=True).stdout
  mono=np.frombuffer(raw,dtype='<f4');hop=120
  rms=np.array([np.sqrt(np.mean(mono[i:i+hop]**2)) for i in range(0,len(mono)-hop,hop)])
  onset=int(np.argmax(np.diff(rms)))+1;start=max(0,rough-.45+onset*.01-.015)
  if duration is None:
   tail=subprocess.run([args.ffmpeg,'-v','error','-ss',str(start),'-i',str(archive),'-map','0:a:0','-vn','-ac','1','-ar',str(RATE),'-f','f32le','pipe:1'],capture_output=True,check=True).stdout
   duration=len(tail)/(4*RATE)
  # Conservative stationary-noise reduction on the microphone recordings only.
  denoise='highpass=f=70,afftdn=nr=8:nf=-48:tn=1:gs=5,lowpass=f=14500,' if recording else ''
  loudness=-15 if name=='TheWorld' else -19
  filters=denoise+f'loudnorm=I={loudness}:TP=-2:LRA=8,afade=t=in:d=0.08,afade=t=out:st={duration-.65}:d=0.65'
  folder=ROOT/'Assets/Audio/Music/Gifts'/name;folder.mkdir(parents=True,exist_ok=True)
  out=folder/'bgm-theme.ogg'
  staged=SOURCE/(name+'-export.ogg')
  subprocess.run([args.ffmpeg,'-v','error','-y','-ss',f'{start:.3f}','-i',str(archive),'-t',str(duration),'-map','0:a:0','-vn','-map_metadata','-1','-af',filters,'-ar',str(RATE),'-ac','2','-c:a','libvorbis','-q:a','8',str(staged)],check=True)
  decoded=subprocess.run([args.ffmpeg,'-v','error','-i',str(staged),'-f','f32le','-ac','2','-ar',str(RATE),'pipe:1'],capture_output=True,check=True)
  x=np.frombuffer(decoded.stdout,dtype='<f4')
  staged.replace(out)
  reports.append(dict(character=name,providedSource=str(original),archivedSource=str(archive.relative_to(ROOT)),sourceSha256=hashlib.sha256(archive.read_bytes()).hexdigest(),startSeconds=round(start,3),duration=round(len(x)/(2*RATE),3),filter=filters,output=str(out.relative_to(ROOT)),peak=float(abs(x).max()),rms=float(np.sqrt(np.mean(x*x))),selection='Kaguya: onset near 5:10 through source end. Others: extended high-energy phrase from previous onset; final musical continuity requires listening review.'))
 (SOURCE/'edits.json').write_text(json.dumps(reports,ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps([{k:r[k] for k in ['character','startSeconds','duration','peak','rms']} for r in reports],indent=2))
if __name__=='__main__':main()

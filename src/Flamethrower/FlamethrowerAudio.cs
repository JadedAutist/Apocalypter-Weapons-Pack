using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ChainsawPrototype {
 public partial class Plugin {
  AudioSource flameAudio,flameIgnitionAudio;
  AudioClip nativeFireLoop,flameIgnitionClip;
  float nextNativeFireSearch,nextFlameAudioWarning,flameAudioLevel;
  bool flameAudioFallback,flameAudioWasFiring;
  int flameAudioFrame=-1;
  const float FlameAudioRise=.04f,FlameAudioRelease=.09f;
  float FlamethrowerAttackVolume(){float volume=flameVolume==null?1f:flameVolume.Value;return float.IsNaN(volume)||float.IsInfinity(volume)?1f:Mathf.Clamp01(volume);}

  void CreateFlamethrowerAudio(Transform parent){
   var go=new GameObject("Flamethrower.AttackAudio");go.transform.SetParent(parent,false);
   flameAudio=go.AddComponent<AudioSource>();ConfigureFlameAudioSource(flameAudio,true);flameAudio.volume=0f;
   flameIgnitionAudio=go.AddComponent<AudioSource>();ConfigureFlameAudioSource(flameIgnitionAudio,false);flameIgnitionAudio.volume=FlamethrowerAttackVolume()*.6f;
   flameAudioLevel=0f;flameAudioWasFiring=false;flameAudioFrame=-1;
  }
  static void ConfigureFlameAudioSource(AudioSource source,bool loop){
   source.playOnAwake=false;source.loop=loop;source.spatialBlend=0f;source.dopplerLevel=0f;source.priority=16;source.pitch=1f;
  }
  void UpdateFlamethrowerAudio(bool firing){
   // The final pose also runs during pre-cull. Fuel ticks and extra cameras
   // must not restart the loop, repeat ignition, or advance the fader twice.
   if(flameAudio==null||flameAudioFrame==Time.frameCount)return;
   flameAudioFrame=Time.frameCount;
   if(Time.timeScale<=0f||Cursor.visible){StopFlamethrowerAudio();return;}
   if((nativeFireLoop==null||flameAudioFallback||flameIgnitionClip==null)&&Time.unscaledTime>=nextNativeFireSearch){nextNativeFireSearch=Time.unscaledTime+3f;PrepareFlamethrowerAudio();}
   float volume=FlamethrowerAttackVolume();
   flameAudio.enabled=true;flameAudio.mute=volume<=0f;flameAudio.priority=16;
   if(flameIgnitionAudio!=null){flameIgnitionAudio.enabled=true;flameIgnitionAudio.mute=volume<=0f;flameIgnitionAudio.volume=volume*.6f;}
   if(flameAudio.clip!=nativeFireLoop){flameAudio.Stop();flameAudio.clip=nativeFireLoop;}
   if(flameIgnitionAudio!=null&&flameIgnitionAudio.clip!=flameIgnitionClip)flameIgnitionAudio.clip=flameIgnitionClip;
   bool ready=nativeFireLoop!=null&&nativeFireLoop.loadState==AudioDataLoadState.Loaded;
   if(firing&&ready&&!flameAudio.isPlaying)flameAudio.Play();
   if(firing&&!flameAudioWasFiring&&flameIgnitionAudio!=null&&flameIgnitionClip!=null)flameIgnitionAudio.Play();
   float target=firing&&ready?1f:0f;
   float duration=target>flameAudioLevel?FlameAudioRise:FlameAudioRelease;
   flameAudioLevel=Mathf.MoveTowards(flameAudioLevel,target,Mathf.Min(Time.unscaledDeltaTime,.1f)/duration);flameAudio.volume=volume*flameAudioLevel;
   if(target==0f&&flameAudioLevel<=.0001f){flameAudio.Stop();if(!firing&&flameIgnitionAudio!=null)flameIgnitionAudio.Stop();}
   flameAudioWasFiring=firing;
   if(firing&&volume>0f&&Time.unscaledTime>=nextFlameAudioWarning&&(!ready||flameAudio.isVirtual||AudioListener.pause||AudioListener.volume<=.00001f)){
    nextFlameAudioWarning=Time.unscaledTime+10f;
    Logger.LogWarning("Flamethrower attack audio: ready="+ready+", virtual="+flameAudio.isVirtual+", listenerPaused="+AudioListener.pause+", listenerVolume="+AudioListener.volume+", attackVolume="+flameAudio.volume);
   }
  }
  void StopFlamethrowerAudio(){
   flameAudioLevel=0f;flameAudioWasFiring=false;
   if(flameAudio!=null){flameAudio.volume=0f;flameAudio.Stop();}
   if(flameIgnitionAudio!=null)flameIgnitionAudio.Stop();
  }
  void CleanupFlamethrowerAudio(){
   StopFlamethrowerAudio();
   // Both clips are private decoded copies; native clips/mixers are untouched.
   if(nativeFireLoop!=null)Destroy(nativeFireLoop);nativeFireLoop=null;
   if(flameIgnitionClip!=null)Destroy(flameIgnitionClip);flameIgnitionClip=null;
  }
  void PrepareFlamethrowerAudio(){
   if(nativeFireLoop==null||flameAudioFallback){
    var prepared=LoadFlameAttackPcm("StoveBurnerLoop",false);
    if(prepared!=null){var old=nativeFireLoop;nativeFireLoop=prepared;flameAudioFallback=false;if(old!=null)Destroy(old);Logger.LogInfo("Flamethrower native portable stove burner loop is ready (gas_stove, original pitch/gain).");}
    else if(nativeFireLoop==null){
     // A compressed/streaming native clip can have a name before its samples
     // are readable. Keep retrying, and never accept empty or invalid PCM.
     var native=Resources.FindObjectsOfTypeAll<AudioClip>().FirstOrDefault(c=>c.name=="gas_stove");
     nativeFireLoop=CopyReadyNativeStove(native,true);flameAudioFallback=true;
    }
   }
   if(flameIgnitionClip==null){flameIgnitionClip=LoadFlameAttackPcm("StoveActivationClick",false);if(flameIgnitionClip==null)flameIgnitionClip=CopyReadyNativeStove(Resources.FindObjectsOfTypeAll<AudioClip>().FirstOrDefault(c=>c.name=="switch1"),false);}
  }
  AudioClip LoadFlameAttackPcm(string name,bool smooth){
   string path=Path.Combine(Path.GetDirectoryName(Info.Location),"Flamethrower","NativeEffects",name+".wav");if(!File.Exists(path))return null;
   try{int channels,frequency;var samples=ReadNativeFirePcm(path,out channels,out frequency);return MakeFlameAttackClip(samples,channels,frequency,"Flamethrower.Attack."+name,smooth);}
   catch(Exception error){if(Time.unscaledTime>=nextFlameAudioWarning){nextFlameAudioWarning=Time.unscaledTime+10f;Logger.LogWarning("Flamethrower attack WAV is not ready: "+name+"; "+error.Message);}return null;}
  }
  AudioClip CopyReadyNativeStove(AudioClip native,bool smooth){
   if(native==null)return null;
   try{
    if(native.loadState!=AudioDataLoadState.Loaded){native.LoadAudioData();if(native.loadState!=AudioDataLoadState.Loaded)return null;}
    if(native.channels<1||native.channels>2||native.frequency<8000||native.samples<256||native.samples>native.frequency*60)return null;
    var samples=new float[checked(native.samples*native.channels)];if(!native.GetData(samples,0))return null;
    return MakeFlameAttackClip(samples,native.channels,native.frequency,"Flamethrower.Attack.NativeStove."+native.name,smooth);
   }catch(Exception){return null;}
  }
  AudioClip MakeFlameAttackClip(float[] samples,int channels,int frequency,string name,bool smooth){
   float peak;double rms;if(!ValidateFlameAudioPcm(samples,channels,frequency,out peak,out rms))return null;
   var output=smooth?SmoothFlameAudioPcm(samples,channels,frequency):samples;
   int frames=output.Length/channels;var clip=AudioClip.Create(name,frames,channels,frequency,false);
   if(!clip.SetData(output,0)){Destroy(clip);return null;}
   clip.hideFlags=HideFlags.DontUnloadUnusedAsset;return clip;
  }
  internal static bool ValidateFlameAudioPcm(float[] samples,int channels,int frequency,out float peak,out double rms){
   peak=0f;rms=0;
   if(samples==null||channels<1||channels>2||frequency<8000||frequency>192000||samples.Length<channels*256||samples.Length%channels!=0)return false;
   foreach(float sample in samples){if(float.IsNaN(sample)||float.IsInfinity(sample)||Math.Abs(sample)>1.001f)return false;peak=Math.Max(peak,Math.Abs(sample));rms+=(double)sample*sample;}
   rms=Math.Sqrt(rms/samples.Length);return peak>.00001f&&rms>.000001;
  }
  internal static float[] SmoothFlameAudioPcm(float[] samples,int channels,int frequency){
   int frames=samples.Length/channels,fade=Math.Min(frequency/25,frames/8),count=frames-fade;var output=new float[count*channels];
   Array.Copy(samples,fade*channels,output,0,(frames-2*fade)*channels);
   for(int frame=0;frame<fade;frame++){float mix=frame/(float)(fade-1);mix=mix*mix*(3-2*mix);for(int channel=0;channel<channels;channel++)output[(count-fade+frame)*channels+channel]=samples[(frames-fade+frame)*channels+channel]*(1-mix)+samples[frame*channels+channel]*mix;}
   // Preserve the native burner gain/timbre. Match the immediate seam without
   // adding pressure, steam, fire layers, pitch changes or level compression.
   int edge=Math.Min(32,count/2);for(int channel=0;channel<channels;channel++){
    float seam=(output[channel]+output[(count-1)*channels+channel])*.5f;
    for(int i=0;i<edge;i++){float mix=1f-i/(float)(edge-1);mix=mix*mix*(3-2*mix);int first=i*channels+channel,last=(count-1-i)*channels+channel;output[first]=output[first]*(1-mix)+seam*mix;output[last]=output[last]*(1-mix)+seam*mix;}
   }return output;
  }
  internal static float[] ReadNativeFirePcm(string path,out int channels,out int frequency){
   channels=0;frequency=0;int bits=0,format=0;byte[] pcm=null;
   using(var file=File.OpenRead(path))using(var reader=new BinaryReader(file)){
    if(new string(reader.ReadChars(4))!="RIFF")throw new InvalidDataException("Native fire loop is not a WAV file.");reader.ReadUInt32();if(new string(reader.ReadChars(4))!="WAVE")throw new InvalidDataException("Native fire loop has no WAVE header.");
    while(file.Position+8<=file.Length){
     string chunk=new string(reader.ReadChars(4));uint length=reader.ReadUInt32();long end=file.Position+length;if(end>file.Length||length>16777216)throw new InvalidDataException("Native fire loop contains an invalid WAV chunk.");
     if(chunk=="fmt "&&length>=16){format=reader.ReadUInt16();channels=reader.ReadUInt16();frequency=reader.ReadInt32();reader.ReadUInt32();reader.ReadUInt16();bits=reader.ReadUInt16();}
     else if(chunk=="data")pcm=reader.ReadBytes(checked((int)length));
     file.Position=end+(length%2);
    }
   }
   if(format!=1||bits!=16||channels<1||channels>2||frequency<8000||frequency>192000||pcm==null||pcm.Length==0||pcm.Length%(channels*2)!=0)throw new InvalidDataException("Native fire loop must contain complete 16-bit PCM samples.");
   var samples=new float[pcm.Length/2];for(int i=0;i<samples.Length;i++)samples[i]=(short)(pcm[i*2]|pcm[i*2+1]<<8)/32768f;return samples;
  }
 }
}

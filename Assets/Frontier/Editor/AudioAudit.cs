using System;
using System.IO;
using UnityEngine;

namespace Frontier.Editor
{
    public static class AudioAudit
    {
        [Serializable] private class Metric
        {public string category;public int samples,frequency,channels;public float seconds,peak,rms,first,last;public string scope="Unity-generated original clip samples; not a live audio mix, listening test or native match";}
        public static void Run()
        {
            string directory=Environment.GetEnvironmentVariable("FRONTIER_REPORT_PATH")??"Builds/AudioAudit";Directory.CreateDirectory(directory);
            using(var report=new StreamWriter(Path.Combine(directory,"metrics.jsonl")))
            foreach(var category in new[]{EventAudio.SoundCategory.Footstep,EventAudio.SoundCategory.Hoof,EventAudio.SoundCategory.Wheel,EventAudio.SoundCategory.Woodcut,EventAudio.SoundCategory.Mine,EventAudio.SoundCategory.Handling,EventAudio.SoundCategory.Build})
            {
                var clip=ActivitySoundClips.Create(category);var data=new float[clip.samples*clip.channels];
                if(!clip.GetData(data,0))throw new InvalidOperationException("Cannot read generated clip");
                float peak=0;double square=0;foreach(float sample in data){if(float.IsNaN(sample)||float.IsInfinity(sample))throw new InvalidOperationException("Nonfinite sample");peak=Mathf.Max(peak,Mathf.Abs(sample));square+=sample*sample;}
                var metric=new Metric{category=category.ToString(),samples=clip.samples,frequency=clip.frequency,channels=clip.channels,seconds=clip.length,peak=peak,rms=(float)Math.Sqrt(square/data.Length),first=data[0],last=data[data.Length-1]};
                if(metric.channels!=1||metric.frequency!=22050||metric.seconds>.35f||metric.peak>.7f||metric.peak<.001f||metric.first!=0||metric.last!=0)throw new InvalidOperationException("Clip shape outside contract");
                report.WriteLine(JsonUtility.ToJson(metric));
                using(var writer=new BinaryWriter(File.Create(Path.Combine(directory,category+".wav"))))
                {
                    int bytes=data.Length*2;
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(bytes+36);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(clip.frequency);writer.Write(clip.frequency*2);writer.Write((short)2);writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(bytes);
                    foreach(float sample in data)writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample,-1,1)*32767));
                }
                UnityEngine.Object.DestroyImmediate(clip);Debug.Log("AUDIO_AUDIT "+JsonUtility.ToJson(metric));
            }
        }
    }
}

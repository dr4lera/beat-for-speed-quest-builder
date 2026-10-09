using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public static class QuestBfs1
{
    [Serializable] public class Header
    {
        public int version, sampleRate, channels, bpmMilli, lanes, offsetUs;
        public long frames;
        public string id, title, scene;
        public Event[] events;
    }
    [Serializable] public class Event { public long tick; public int lane, type; public float[] color; }
    public static bool IsPacked(string path)
    {
        using(var reader=new BinaryReader(File.OpenRead(path))) return reader.BaseStream.Length>=4 && reader.ReadUInt32()==0x31534642;
    }
    public static void Extract(string pack,string dest)
    {
        using(var input=new BinaryReader(File.OpenRead(pack)))
        {
            if(input.ReadUInt32()!=0x31534642) throw new InvalidDataException("Unknown BFS container");
            int headerLength=input.ReadInt32(); long audioLength=input.ReadInt64();
            if(headerLength<2 || headerLength>8*1024*1024 || audioLength<2 || audioLength>256L*1024*1024 || input.BaseStream.Length!=16L+headerLength+audioLength)
                throw new InvalidDataException("Invalid BFS1 container lengths");
            var header=JsonUtility.FromJson<Header>(Encoding.UTF8.GetString(input.ReadBytes(headerLength)));
            if(header==null || header.version!=1 || header.sampleRate<8000 || header.sampleRate>192000 || header.channels<1 || header.channels>2 ||
                header.frames<1 || header.frames>int.MaxValue || header.frames*header.channels*2!=audioLength || header.events==null || header.events.Length>50000 ||
                header.bpmMilli<20000 || header.bpmMilli>400000 || header.lanes<1 || header.lanes>11)
                throw new InvalidDataException("Unsupported or invalid BFS1 audio/chart metadata");
            var chart=new QuestChart { musicData=new QuestMusic { bpm=header.bpmMilli/1000f, offset=header.offsetUs/1000000f, trackId=header.scene },
                bfsMetadata=new QuestMetadata { songName=header.title, songId=header.id } };
            chart.entities=header.events.Select(e=> {
                if(e.tick<0 || e.tick>48000000 || e.lane<0 || e.lane>=header.lanes || e.color!=null && e.color.Any(c=>float.IsNaN(c)||float.IsInfinity(c)))
                    throw new InvalidDataException("Invalid BFS1 chart event");
                string type=e.type==0 ? "spawn_cube" : e.type==2 ? "spawn_sting" : e.type==5 ? "color_rgb" : "experimental_"+e.type;
                var tint=e.color!=null && e.color.Length>=3 ? new Color(Mathf.Clamp01(e.color[0]),Mathf.Clamp01(e.color[1]),Mathf.Clamp01(e.color[2]),1) : Color.clear;
                return new QuestEntity { beat=e.tick/960f, key=7+e.lane-header.lanes/2, datamodel="bfs1/"+type, tint=tint };
            }).ToArray();
            string chartJson=JsonUtility.ToJson(chart); QuestSongImport.Parse(chartJson);
            string staging=dest+".pending"; Directory.CreateDirectory(staging);
            File.WriteAllText(Path.Combine(staging,"chart.json"),chartJson);
            using(var output=new BinaryWriter(File.Create(Path.Combine(staging,"audio.wav"))))
            {
                output.Write(Encoding.ASCII.GetBytes("RIFF")); output.Write((uint)(audioLength+36)); output.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                output.Write(16); output.Write((ushort)1); output.Write((ushort)header.channels); output.Write(header.sampleRate);
                output.Write(header.sampleRate*header.channels*2); output.Write((ushort)(header.channels*2)); output.Write((ushort)16);
                output.Write(Encoding.ASCII.GetBytes("data")); output.Write((uint)audioLength);
                byte[] buffer=new byte[65536]; long remaining=audioLength;
                while(remaining>0)
                {
                    int count=input.Read(buffer,0,(int)Math.Min(buffer.Length,remaining));
                    if(count==0) throw new EndOfStreamException("Truncated BFS1 audio"); output.Write(buffer,0,count); remaining-=count;
                }
            }
            Directory.Move(staging,dest);
        }
    }
}

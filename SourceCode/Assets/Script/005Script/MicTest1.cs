﻿using UnityEngine;
using System.Net;
using System.IO;
using System.Text;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class MicTest1 : MonoBehaviour {
    public class postObj
    {
        public string format { get; set; }
        public int rate { get; set; }
        public int channel { get; set; }
        public string token { get; set; }
        public string lan { get; set; }
        public string cuid { get; set; }
        public int len { get; set; }
        public string speech { get; set; }
    }

    public AudioSource aud;
    private string mcName;
    private int currP, lastP;
    private int recStart;
    public bool isRec = false, Speaking = false;
    public float voiceLevel = 0.001f;
    public bool isSpeak = false;
    public float time;
    public const float deltaT = 0.2f;
	public string current;


    private int timeback = 0;
	public bool recognized = false;

    // Use this for initialization

    void Start () {
        aud = GetComponent<AudioSource>();
        time = deltaT;
    }
	
	// Update is called once per frame
	void Update () {
        time -= Time.deltaTime;
        if (time < 0)
        {
            time = deltaT;

            if (isRec)
            {
                lastP = currP;
                currP = Microphone.GetPosition(mcName);
                int audioL = currP - lastP;
                if (audioL <= 0)
                    return;
                float[] tickAudio = new float[audioL - 1];
                aud.clip.GetData(tickAudio, lastP);
                float loudness = coculateLoud(tickAudio);
                isSpeak = loudness > voiceLevel;
                //Debug.Log(loudness);
                if (!isSpeak)
                {
                    if (Speaking)
                    {
                        timeback = 3;
                        Speaking = false;
                    }
                }
                else
                {
                    if (!Speaking)
                    {
                        Speaking = true;
                        recStart = lastP-4000;
                        if (recStart < 0)
                            recStart = 0;
                    }
                }

                if (timeback-- == 1)
                {
                    timeback = 0;
                    float[] talk = new float[currP - recStart - 1];
                    aud.clip.GetData(talk, recStart);
                    recognized = this.play(talk);
                }
            }
        }
	}

    public float coculateLoud(float[] arry)
    {
        float ret = 0;
        foreach(float i in arry)
        {
            ret += i;
        }
        ret /= arry.Length;
        return ret;
    }

    public void startrec()
    {
        Microphone.End(null);
        aud.clip = Microphone.Start(Microphone.devices[0], true, 600, 8000);
        mcName = Microphone.devices[0];
        currP = 0;
        isRec = true;
    }
    public void endrec()
    {
        isRec = false;
        Microphone.End(Microphone.devices[0]);
    }

    public bool play(float[] audio)
    {

        /*var byteArray = new byte[audio.Length * 4];
        Buffer.BlockCopy(audio, 0, byteArray, 0, byteArray.Length);
        string base64str = System.Convert.ToBase64String(byteArray);*/

        Int16[] intData = new Int16[audio.Length];

        Byte[] bytesData = new Byte[audio.Length * 2];
        float sumf = 0;

        int rescaleFactor = 32767; //to convert float to Int16  

        for (int i = 0; i < audio.Length; i++)
        {
            intData[i] = (short)(audio[i] * rescaleFactor);
            Byte[] byteArr = new Byte[2];
            byteArr = BitConverter.GetBytes(intData[i]);
            byteArr.CopyTo(bytesData, i * 2);
            sumf += Math.Abs(audio[i]);
        }
        sumf /= audio.Length;

        Stream fileStream = CreateEmpty();
        fileStream.Write(bytesData, 0, bytesData.Length);
        WriteHeader(fileStream, aud.clip);
        byte[] lastbyte = new byte[fileStream.Length];
        fileStream.Read(lastbyte, 0, lastbyte.Length);

        string base64str = System.Convert.ToBase64String(lastbyte);

        string res = request("http://vop.baidu.com/server_api", base64str, lastbyte.Length);

        Debug.Log(res);
		res.Replace("\"","\'");
		JArray ja =(JArray) JsonConvert.DeserializeObject("[" + res + "]");  

		//Debug.Log(ja [0] ["result"]);

		if (ja [0] ["result"] != null) {
			int j = 0;
			string str = ja [0] ["result"].ToString ();

			foreach (char ch in str) {
				if (ch.Equals ('"'))
					j++;
			}
			j = j / 2;

			for (int i = 0; i < j; i++) {
				str = ja [0] ["result"] [i].ToString ();
				if (str.IndexOf (current) > -1) {
					Debug.Log ("YOU GOT IT!!!");
					return true;
				}
			}
		}
		return false;
    }

    public static string request(string url, string base64audio,int length)
    {

        postObj jsonObj = new postObj()
        {
            format = "wav",
            rate = 8000,
            channel = 1,
            lan="en",
			token = "24.4833e832bfb62b96e8db0a5f2b267fce.2592000.1465225929.282335-8101390",
            cuid = "myUnitySpeech",
            len=length,
            speech=base64audio
        };

        string strJson= JsonConvert.SerializeObject(jsonObj, Formatting.Indented);

        string strURL = url;
        System.Net.HttpWebRequest request;
        request = (System.Net.HttpWebRequest)WebRequest.Create(strURL);
        request.Method = "POST";
        // 添加header
        request.Headers.Add("apikey", "eac9995366c00e5d0025e8ee88e85dbf");
        request.ContentType = "application/json";
        
        byte[] payload;

        payload = System.Text.Encoding.UTF8.GetBytes(strJson);
        request.ContentLength = payload.Length;
        
        Stream writer = request.GetRequestStream();
        writer.Write(payload, 0, payload.Length);
        writer.Close();
        System.Net.HttpWebResponse response = (System.Net.HttpWebResponse)request.GetResponse();
        System.IO.Stream s;
        s = response.GetResponseStream();
        string StrDate = "";
        string strValue = "";
        StreamReader Reader = new StreamReader(s, Encoding.UTF8);
        while ((StrDate = Reader.ReadLine()) != null)
        {
            strValue += StrDate + "\r\n";
        }
        return strValue;
    }

    private static void WriteHeader(Stream stream, AudioClip clip)
    {
        int hz = clip.frequency;
        int channels = clip.channels;
        int samples = clip.samples;

        stream.Seek(0, SeekOrigin.Begin);

        Byte[] riff = System.Text.Encoding.UTF8.GetBytes("RIFF");
        stream.Write(riff, 0, 4);

        Byte[] chunkSize = BitConverter.GetBytes(stream.Length - 8);
        stream.Write(chunkSize, 0, 4);

        Byte[] wave = System.Text.Encoding.UTF8.GetBytes("WAVE");
        stream.Write(wave, 0, 4);

        Byte[] fmt = System.Text.Encoding.UTF8.GetBytes("fmt ");
        stream.Write(fmt, 0, 4);

        Byte[] subChunk1 = BitConverter.GetBytes(16);
        stream.Write(subChunk1, 0, 4);

        UInt16 two = 2;
        UInt16 one = 1;

        Byte[] audioFormat = BitConverter.GetBytes(one);
        stream.Write(audioFormat, 0, 2);

        Byte[] numChannels = BitConverter.GetBytes(channels);
        stream.Write(numChannels, 0, 2);

        Byte[] sampleRate = BitConverter.GetBytes(hz);
        stream.Write(sampleRate, 0, 4);

        Byte[] byteRate = BitConverter.GetBytes(hz * channels * 2); // sampleRate * bytesPerSample*number of channels, here 44100*2*2  
        stream.Write(byteRate, 0, 4);

        UInt16 blockAlign = (ushort)(channels * 2);
        stream.Write(BitConverter.GetBytes(blockAlign), 0, 2);

        UInt16 bps = 16;
        Byte[] bitsPerSample = BitConverter.GetBytes(bps);
        stream.Write(bitsPerSample, 0, 2);

        Byte[] datastring = System.Text.Encoding.UTF8.GetBytes("data");
        stream.Write(datastring, 0, 4);

        Byte[] subChunk2 = BitConverter.GetBytes(samples * channels * 2);
        stream.Write(subChunk2, 0, 4);

    }

    private static Stream CreateEmpty()
    {
        Stream fileStream = new MemoryStream();
        byte emptyByte = new byte();

        for (int i = 0; i < 44; i++) //preparing the header  
        {
            fileStream.WriteByte(emptyByte);
        }

        return fileStream;
    }

}


/*
access_token
:
"24.586bca0b3411d62559ba3daf80bb7fe9.2592000.1463824780.282335-8035270"

expires_in
:
2592000

refresh_token
:
"25.7b2ddbea4f62739c889a38ceb534bc78.315360000.1776592780.282335-8035270"

scope
:
"public audio_voice_assistant_get audio_tts_post wise_adapt lebo_resource_base lightservice_public hetu_basic lightcms_map_poi kaidian_kaidian"

session_key
:
"9mzdDorApudARueBmcFGOSW2ceet594nDnL3A+IamvoX7RtEFDnXCkVryxY8I9FdNGT7ixfb57BuXDQJJiUEJGqHfd7S"

session_secret
:
"a1f5417509b6c98053ff50c7b8a559f0"
*/

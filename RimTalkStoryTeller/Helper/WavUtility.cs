using System;
using UnityEngine;

public static class WavUtility
{
    public static AudioClip ToAudioClip(byte[] wavData, string name)
    {
        int channels = BitConverter.ToInt16(wavData, 22);
        int sampleRate = BitConverter.ToInt32(wavData, 24);
        int dataStart = 44;

        int samples = (wavData.Length - dataStart) / 2;
        float[] audioData = new float[samples];

        int offset = dataStart;
        for (int i = 0; i < samples; i++)
        {
            short sample = BitConverter.ToInt16(wavData, offset);
            audioData[i] = sample / 32768f;
            offset += 2;
        }

        AudioClip clip = AudioClip.Create(
            name, samples, channels, sampleRate, false);

        clip.SetData(audioData, 0);
        return clip;
    }
}
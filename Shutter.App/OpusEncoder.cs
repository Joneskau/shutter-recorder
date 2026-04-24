using System;
using System.IO;
using System.Threading.Tasks;
using Concentus.Enums;
using Concentus.Oggfile;
using NAudio.Wave;
using Shutter.Core;

namespace Shutter.App;

public class OpusEncoder : IEncoder
{
    public Task<string> EncodeAsync(string wavPath, string outputPath, QualityPreset preset)
    {
        return Task.Run(() =>
        {
            var bitRate = preset switch
            {
                QualityPreset.Low => 32000,
                QualityPreset.High => 128000,
                _ => 64000
            };

            using var reader = new AudioFileReader(wavPath);
            var outFormat = new WaveFormat(48000, 16, reader.WaveFormat.Channels);
            using var resampler = new MediaFoundationResampler(reader, outFormat);
            resampler.ResamplerQuality = 60;

            var encoder = Concentus.Structs.OpusEncoder.Create(48000, outFormat.Channels, OpusApplication.OPUS_APPLICATION_AUDIO);
            encoder.Bitrate = bitRate;

            using var fileOut = new FileStream(outputPath, FileMode.Create);
            var oggOut = new OpusOggWriteStream(encoder, fileOut);

            int frameSize = 48000 * 20 / 1000; // 20ms frame (960 samples per channel)
            int bufferSize = frameSize * outFormat.Channels * 2; // 2 bytes per sample
            byte[] buffer = new byte[bufferSize];
            short[] shortBuffer = new short[frameSize * outFormat.Channels];

            int bytesRead;
            while ((bytesRead = resampler.Read(buffer, 0, bufferSize)) == bufferSize)
            {
                Buffer.BlockCopy(buffer, 0, shortBuffer, 0, bytesRead);
                oggOut.WriteSamples(shortBuffer, 0, frameSize);
            }

            if (bytesRead > 0)
            {
                Array.Clear(shortBuffer, 0, shortBuffer.Length);
                Buffer.BlockCopy(buffer, 0, shortBuffer, 0, bytesRead);
                oggOut.WriteSamples(shortBuffer, 0, frameSize);
            }

            oggOut.Finish();

            return outputPath;
        });
    }
}

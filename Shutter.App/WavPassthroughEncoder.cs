using System.IO;
using System.Threading.Tasks;
using Shutter.Core;

namespace Shutter.App;

public class WavPassthroughEncoder : IEncoder
{
    public Task<string> EncodeAsync(string wavPath, string outputPath, QualityPreset preset)
    {
        return Task.Run(() =>
        {
            var targetSampleRate = preset switch
            {
                QualityPreset.Low => 22050,
                QualityPreset.High => 48000,
                _ => 44100
            };

            using var reader = new NAudio.Wave.AudioFileReader(wavPath);
            if (reader.WaveFormat.SampleRate == targetSampleRate)
            {
                if (wavPath != outputPath)
                {
                    File.Copy(wavPath, outputPath, true);
                }
            }
            else
            {
                var outFormat = new NAudio.Wave.WaveFormat(targetSampleRate, reader.WaveFormat.Channels);
                using var resampler = new NAudio.Wave.MediaFoundationResampler(reader, outFormat);
                resampler.ResamplerQuality = 60;
                NAudio.Wave.WaveFileWriter.CreateWaveFile(outputPath, resampler);
            }

            return outputPath;
        });
    }
}

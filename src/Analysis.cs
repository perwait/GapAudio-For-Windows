using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace GapGone {
    public static class Analysis {
        // Original algorithm: root mean square energy. All channels and the actual last window length are included.
        public static List<Region> Silence(AudioData data, double threshold, double minimum, double padding, bool edgesOnly, CancellationToken token = default(CancellationToken)) {
            int channels = data.Info.Channels, rate = data.Info.SampleRate;
            int window = Math.Max(1, (int)(rate * 0.02));
            var raw = new List<Region>(); double start = -1;
            var buffer = new double[window * channels];
            for (long f = 0; f < data.Frames; f += window) {
                token.ThrowIfCancellationRequested();
                int count = (int)Math.Min(window, data.Frames - f); double sum = 0;
                data.ReadSamples(f * channels, buffer, count * channels);
                for (int j = 0; j < count * channels; j++) sum += buffer[j] * buffer[j];
                bool silent = Math.Sqrt(sum / (count * channels)) < threshold;
                if (silent && start < 0) start = (double)f / rate;
                if (!silent && start >= 0) { raw.Add(new Region(start, (double)f / rate)); start = -1; }
            }
            if (start >= 0) raw.Add(new Region(start, (double)data.Frames / rate));
            var result = new List<Region>();
            double duration = (double)data.Frames / rate;
            foreach (var r in raw) {
                bool leading = r.Start < 0.0001, trailing = r.End >= duration - 0.0001;
                if (edgesOnly && !leading && !trailing) continue;
                if (r.End - r.Start < minimum) continue;
                // Entirely silent audio keeps at least the requested padding at either end.
                double a = leading ? r.Start : r.Start + padding, b = trailing ? r.End : r.End - padding;
                if (leading && trailing) { a = padding; b = duration - padding; }
                else if (leading) b = r.End - padding;
                else if (trailing) a = r.Start + padding;
                if (b > a) result.Add(new Region(a, b));
            }
            return Regions.Normalize(result, duration);
        }
        static void Fft(Complex[] x) {
            int n = x.Length;
            for (int i = 1, j = 0; i < n; i++) {
                int bit = n >> 1; for (; (j & bit) != 0; bit >>= 1) j ^= bit; j ^= bit;
                if (i < j) { var t = x[i]; x[i] = x[j]; x[j] = t; }
            }
            for (int len = 2; len <= n; len <<= 1) {
                var wlen = Complex.FromPolarCoordinates(1, -2 * Math.PI / len);
                for (int i = 0; i < n; i += len) {
                    var w = Complex.One;
                    for (int j = 0; j < len / 2; j++) { var u = x[i + j]; var v = x[i + j + len / 2] * w; x[i + j] = u + v; x[i + j + len / 2] = u - v; w *= wlen; }
                }
            }
        }
        public static FrequencyRange MeasureFrequencyRange(AudioData data, CancellationToken token) {
            const int n = 8192; var power = new double[n / 2 + 1];
            // Distribute FFT windows across the whole recording; use each channel separately to avoid phase cancellation.
            int windowCount = (int)Math.Min(96, Math.Max(1, data.Frames / n));
            for (int w = 0; w < windowCount; w++) {
                token.ThrowIfCancellationRequested();
                long start = windowCount == 1 ? 0 : Math.Max(0, data.Frames - n) * w / (windowCount - 1);
                var samples = new double[n * data.Info.Channels]; data.ReadSamples(start * data.Info.Channels, samples, (int)Math.Min(n, data.Frames-start) * data.Info.Channels);
                for (int c = 0; c < data.Info.Channels; c++) {
                    var x = new Complex[n]; double energy = 0;
                    for (int j = 0; j < n && start + j < data.Frames; j++) { double v = samples[j * data.Info.Channels + c]; energy += v * v; x[j] = new Complex(v * (0.5 - 0.5 * Math.Cos(2 * Math.PI * j / (n - 1))), 0); }
                    if (energy / n < 1e-10) continue;
                    Fft(x);
                    for (int j = 1; j < power.Length; j++) power[j] += x[j].Magnitude * x[j].Magnitude;
                }
            }
            double max = power.Max(), minHz = 0, maxHz = 0;
            // Frequencies with energy >= -60 dB relative to the strongest bin. These are measured significant bands, not sampling limits.
            if (max > 0) for (int j = 1; j < power.Length; j++) { double hz = (double)j * data.Info.SampleRate / n; if (power[j] >= max * 1e-6) { if (minHz == 0) minHz = hz; maxHz = hz; } }
            return new FrequencyRange { MinHz = minHz, MaxHz = maxHz };
        }
    }
}

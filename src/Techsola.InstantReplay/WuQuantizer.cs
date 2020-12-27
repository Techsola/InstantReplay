using System;
using System.Collections.Generic;

namespace Techsola.InstantReplay
{
    // TODO: Implement WSM-WU from https://arxiv.org/pdf/1101.0395.pdf. (Run Wu's algorithm to initialize cluster
    // centers, then run the Weighted Sort-Means algorithm.)

    // Also consider converting the color space from RGB to CIELAB to do the quantizing, then back.

    /// <summary>
    /// Implements <see href="https://www.ece.mcmaster.ca/~xwu/cq.c"/> by Xiaolin Wu.
    /// </summary>
    internal sealed class WuQuantizer
    {
        private const int MaxColorCount = 256;
        private const int HistogramChannelSizeLog2 = 5;
        private const int HistogramChannelSize = 1 << HistogramChannelSizeLog2;
        private const int ChannelIndexShift = 8 - HistogramChannelSizeLog2;

        private readonly MomentStatistics[,,] moments = new MomentStatistics[HistogramChannelSize + 1, HistogramChannelSize + 1, HistogramChannelSize + 1];
        private readonly byte[,,] tag = new byte[HistogramChannelSize + 1, HistogramChannelSize + 1, HistogramChannelSize + 1];

        // Referenced http://inis.jinr.ru/sl/vol1/CMC/Graphics_Gems_2,ed_J.Arvo.pdf and
        // https://github.com/JeremyAnsel/JeremyAnsel.ColorQuant/blob/a025932f7ec361337aaab3057608ed0f71e4e781/JeremyAnsel.ColorQuant/JeremyAnsel.ColorQuant/WuColorQuantizer.cs
        // to help figure out what was going on.

        public void Quantize(
            ColorEnumerable sourceImage,
            (byte R, byte G, byte B)[] paletteBuffer,
            out int paletteLength,
            byte[] indexedImageBuffer)
        {
            if (paletteBuffer.Length != MaxColorCount)
                throw new ArgumentException($"Palette buffer must be equal to the maximum color count {MaxColorCount}.");

            InitializeAs3DHistogram(sourceImage);

            ComputeCumulativeMoments();

            var cubes = Partition();

            OutputPalette(cubes, paletteBuffer, out paletteLength);
            OutputIndexedPixels(sourceImage, cubes, indexedImageBuffer);
        }

        private void OutputIndexedPixels(ColorEnumerable sourceImage, Box[] cubes, byte[] indexedImageBuffer)
        {
            for (var paletteIndex = 0; paletteIndex < cubes.Length; paletteIndex++)
            {
                ref readonly var cube = ref cubes[paletteIndex];

                for (var channel1 = cube.Channel1.Bottom + 1; channel1 <= cube.Channel1.Top; channel1++)
                    for (var channel2 = cube.Channel2.Bottom + 1; channel2 <= cube.Channel2.Top; channel2++)
                        for (var channel3 = cube.Channel3.Bottom + 1; channel3 <= cube.Channel3.Top; channel3++)
                            tag[channel1, channel2, channel3] = (byte)paletteIndex;
            }

            var i = 0;
            foreach (var (channel1, channel2, channel3) in sourceImage)
            {
                indexedImageBuffer[i] = tag[
                    (channel1 >> ChannelIndexShift) + 1,
                    (channel2 >> ChannelIndexShift) + 1,
                    (channel3 >> ChannelIndexShift) + 1];
                i++;
            }
        }

        private void OutputPalette(Box[] cubes, (byte R, byte G, byte B)[] paletteBuffer, out int paletteLength)
        {
            paletteLength = cubes.Length;

            for (var i = 0; i < cubes.Length; i++)
            {
                var volume = GetVolume(in cubes[i]);

                paletteBuffer[i] = (
                    (byte)(volume.Channel1TimesDensity / volume.Density),
                    (byte)(volume.Channel2TimesDensity / volume.Density),
                    (byte)(volume.Channel3TimesDensity / volume.Density));
            }
        }

        private Box[] Partition()
        {
            var cubes = new List<Box>(capacity: MaxColorCount)
            {
                new()
                {
                    Channel1 = { Top = HistogramChannelSize },
                    Channel2 = { Top = HistogramChannelSize },
                    Channel3 = { Top = HistogramChannelSize },
                },
            };

            var variances = new List<float>(capacity: MaxColorCount) { 0 };
            var next = 0;

            while (cubes.Count < MaxColorCount)
            {
                if (Cut(cubes[next]) is var (newBottom, newTop))
                {
                    cubes[next] = newBottom;
                    variances[next] = newBottom.Volume > 1 ? GetWeightedVariance(in newBottom) : 0;

                    cubes.Add(newTop);
                    variances.Add(newTop.Volume > 1 ? GetWeightedVariance(in newTop) : 0);
                }
                else
                {
                    variances[next] = 0; // Don't try to split this box again
                }

                next = 0;
                var maxVariance = variances[0];

                for (var k = 1; k < variances.Count; k++)
                {
                    if (maxVariance < variances[k])
                    {
                        maxVariance = variances[k];
                        next = k;
                    }
                }

                if (maxVariance <= 0) break;
            }

            return cubes.ToArray();
        }

        private void InitializeAs3DHistogram(ColorEnumerable sourceImage)
        {
            Array.Clear(moments, 0, moments.Length);

            foreach (var (channel1, channel2, channel3) in sourceImage)
            {
                ref var latticePoint = ref moments[
                    (channel1 >> ChannelIndexShift) + 1,
                    (channel2 >> ChannelIndexShift) + 1,
                    (channel3 >> ChannelIndexShift) + 1];

                latticePoint.Density++;
                latticePoint.Channel1TimesDensity += channel1;
                latticePoint.Channel2TimesDensity += channel2;
                latticePoint.Channel3TimesDensity += channel3;
                latticePoint.MagnitudeSquaredTimesDensity += (channel1 * channel1) + (channel2 * channel2) + (channel3 * channel3);
            }
        }

        private void ComputeCumulativeMoments()
        {
            var areaByChannel3 = new MomentStatistics[HistogramChannelSize + 1];

            for (var channel1 = 1; channel1 <= HistogramChannelSize; channel1++)
            {
                Array.Clear(areaByChannel3, 0, areaByChannel3.Length);

                for (var channel2 = 1; channel2 <= HistogramChannelSize; channel2++)
                {
                    var line = default(MomentStatistics);

                    for (var channel3 = 1; channel3 <= HistogramChannelSize; channel3++)
                    {
                        ref var latticePoint = ref moments[channel1, channel2, channel3];
                        line += latticePoint;

                        ref var area = ref areaByChannel3[channel3];
                        area += line;

                        latticePoint = moments[channel1 - 1, channel2, channel3] + area;
                    }
                }
            }
        }

        private MomentStatistics GetVolume(in Box cube)
        {
            return
                moments[cube.Channel1.Top, cube.Channel2.Top, cube.Channel3.Top]
                - moments[cube.Channel1.Top, cube.Channel2.Top, cube.Channel3.Bottom]
                - moments[cube.Channel1.Top, cube.Channel2.Bottom, cube.Channel3.Top]
                + moments[cube.Channel1.Top, cube.Channel2.Bottom, cube.Channel3.Bottom]
                - moments[cube.Channel1.Bottom, cube.Channel2.Top, cube.Channel3.Top]
                + moments[cube.Channel1.Bottom, cube.Channel2.Top, cube.Channel3.Bottom]
                + moments[cube.Channel1.Bottom, cube.Channel2.Bottom, cube.Channel3.Top]
                - moments[cube.Channel1.Bottom, cube.Channel2.Bottom, cube.Channel3.Bottom];
        }

        private MomentStatistics GetBottom(in Box cube, Direction direction)
        {
            return direction switch
            {
                Direction.Channel1 =>
                    -moments[cube.Channel1.Bottom, cube.Channel2.Top, cube.Channel3.Top]
                    + moments[cube.Channel1.Bottom, cube.Channel2.Top, cube.Channel3.Bottom]
                    + moments[cube.Channel1.Bottom, cube.Channel2.Bottom, cube.Channel3.Top]
                    - moments[cube.Channel1.Bottom, cube.Channel2.Bottom, cube.Channel3.Bottom],

                Direction.Channel2 =>
                    -moments[cube.Channel1.Top, cube.Channel2.Bottom, cube.Channel3.Top]
                    + moments[cube.Channel1.Top, cube.Channel2.Bottom, cube.Channel3.Bottom]
                    + moments[cube.Channel1.Bottom, cube.Channel2.Bottom, cube.Channel3.Top]
                    - moments[cube.Channel1.Bottom, cube.Channel2.Bottom, cube.Channel3.Bottom],

                Direction.Channel3 =>
                    -moments[cube.Channel1.Top, cube.Channel2.Top, cube.Channel3.Bottom]
                    + moments[cube.Channel1.Top, cube.Channel2.Bottom, cube.Channel3.Bottom]
                    + moments[cube.Channel1.Bottom, cube.Channel2.Top, cube.Channel3.Bottom]
                    - moments[cube.Channel1.Bottom, cube.Channel2.Bottom, cube.Channel3.Bottom],
            };
        }

        private MomentStatistics GetTop(in Box cube, Direction direction, int position)
        {
            return direction switch
            {
                Direction.Channel1 =>
                    moments[position, cube.Channel2.Top, cube.Channel3.Top]
                    - moments[position, cube.Channel2.Top, cube.Channel3.Bottom]
                    - moments[position, cube.Channel2.Bottom, cube.Channel3.Top]
                    + moments[position, cube.Channel2.Bottom, cube.Channel3.Bottom],

                Direction.Channel2 =>
                    moments[cube.Channel1.Top, position, cube.Channel3.Top]
                    - moments[cube.Channel1.Top, position, cube.Channel3.Bottom]
                    - moments[cube.Channel1.Bottom, position, cube.Channel3.Top]
                    + moments[cube.Channel1.Bottom, position, cube.Channel3.Bottom],

                Direction.Channel3 =>
                    moments[cube.Channel1.Top, cube.Channel2.Top, position]
                    - moments[cube.Channel1.Top, cube.Channel2.Bottom, position]
                    - moments[cube.Channel1.Bottom, cube.Channel2.Top, position]
                    + moments[cube.Channel1.Bottom, cube.Channel2.Bottom, position],
            };
        }

        private float GetWeightedVariance(in Box cube)
        {
            var volume = GetVolume(in cube);

            return volume.MagnitudeSquaredTimesDensity - volume.GetSumOfChannelsSquaredOverDensity();
        }

        private (float Max, int Cut) Maximize(in Box cube, Direction direction, int first, int last, in MomentStatistics whole)
        {
            var bottom = GetBottom(in cube, direction);

            var max = 0f;
            var cut = -1;

            for (var position = first; position < last; position++)
            {
                var half = bottom + GetTop(in cube, direction, position);
                if (half.Density == 0) continue;

                var temp = half.GetSumOfChannelsSquaredOverDensity();

                half = whole - half;
                if (half.Density == 0) continue;

                temp += half.GetSumOfChannelsSquaredOverDensity();

                if (max < temp)
                {
                    max = temp;
                    cut = position;
                }
            }

            return (max, cut);
        }

        private (Box Bottom, Box Top)? Cut(Box cube)
        {
            var whole = GetVolume(in cube);

            var channel1 = Maximize(in cube, Direction.Channel1, first: cube.Channel1.Bottom + 1, last: cube.Channel1.Top, in whole);
            var channel2 = Maximize(in cube, Direction.Channel2, first: cube.Channel2.Bottom + 1, last: cube.Channel2.Top, in whole);
            var channel3 = Maximize(in cube, Direction.Channel3, first: cube.Channel3.Bottom + 1, last: cube.Channel3.Top, in whole);

            var newCube = default(Box);

            newCube.Channel1.Top = cube.Channel1.Top;
            newCube.Channel2.Top = cube.Channel2.Top;
            newCube.Channel3.Top = cube.Channel3.Top;

            if (channel1.Max >= channel2.Max && channel1.Max >= channel3.Max)
            {
                if (channel1.Cut < 0) return null;

                newCube.Channel1.Bottom = cube.Channel1.Top = channel1.Cut;
                newCube.Channel2.Bottom = cube.Channel2.Bottom;
                newCube.Channel3.Bottom = cube.Channel3.Bottom;
            }
            else if (channel2.Max >= channel1.Max && channel2.Max >= channel3.Max)
            {
                newCube.Channel1.Bottom = cube.Channel1.Bottom;
                newCube.Channel2.Bottom = cube.Channel2.Top = channel2.Cut;
                newCube.Channel3.Bottom = cube.Channel3.Bottom;
            }
            else
            {
                newCube.Channel1.Bottom = cube.Channel1.Bottom;
                newCube.Channel2.Bottom = cube.Channel2.Bottom;
                newCube.Channel3.Bottom = cube.Channel3.Top = channel3.Cut;
            }

            cube.CalculateVolume();
            newCube.CalculateVolume();

            return (Bottom: cube, Top: newCube);
        }

        private enum Direction
        {
            Channel1,
            Channel2,
            Channel3,
        }

        private struct MomentStatistics
        {
            /// <summary><c>P(c)</c></summary>
            public int Density;
            /// <summary><c>Channel1 × P(c)</c></summary>
            public int Channel1TimesDensity;
            /// <summary><c>Channel2 × P(c)</c></summary>
            public int Channel2TimesDensity;
            /// <summary><c>Channel3 × P(c)</c></summary>
            public int Channel3TimesDensity;
            /// <summary><c>c² × P(c)</c></summary>
            public float MagnitudeSquaredTimesDensity;

            public float GetSumOfChannelsSquaredOverDensity()
            {
                return (
                    (float)Channel1TimesDensity * Channel1TimesDensity
                    + (float)Channel2TimesDensity * Channel2TimesDensity
                    + (float)Channel3TimesDensity * Channel3TimesDensity)
                    / Density;
            }

            public static MomentStatistics operator +(in MomentStatistics left, in MomentStatistics right)
            {
                return new()
                {
                    Density = left.Density + right.Density,
                    Channel1TimesDensity = left.Channel1TimesDensity + right.Channel1TimesDensity,
                    Channel2TimesDensity = left.Channel2TimesDensity + right.Channel2TimesDensity,
                    Channel3TimesDensity = left.Channel3TimesDensity + right.Channel3TimesDensity,
                    MagnitudeSquaredTimesDensity = left.MagnitudeSquaredTimesDensity + right.MagnitudeSquaredTimesDensity,
                };
            }

            public static MomentStatistics operator -(in MomentStatistics left, in MomentStatistics right)
            {
                return new()
                {
                    Density = left.Density - right.Density,
                    Channel1TimesDensity = left.Channel1TimesDensity - right.Channel1TimesDensity,
                    Channel2TimesDensity = left.Channel2TimesDensity - right.Channel2TimesDensity,
                    Channel3TimesDensity = left.Channel3TimesDensity - right.Channel3TimesDensity,
                    MagnitudeSquaredTimesDensity = left.MagnitudeSquaredTimesDensity - right.MagnitudeSquaredTimesDensity,
                };
            }

            public static MomentStatistics operator -(in MomentStatistics statistics)
            {
                return new()
                {
                    Density = -statistics.Density,
                    Channel1TimesDensity = -statistics.Channel1TimesDensity,
                    Channel2TimesDensity = -statistics.Channel2TimesDensity,
                    Channel3TimesDensity = -statistics.Channel3TimesDensity,
                    MagnitudeSquaredTimesDensity = -statistics.MagnitudeSquaredTimesDensity,
                };
            }
        }

        private struct Range
        {
            /// <summary>Exclusive minimum.</summary>
            public int Bottom;
            /// <summary>Inclusive maximum.</summary>
            public int Top;

            public int Length => Top - Bottom;
        }

        private struct Box
        {
            public Range Channel1;
            public Range Channel2;
            public Range Channel3;
            public int Volume;

            public void CalculateVolume()
            {
                Volume = Channel1.Length * Channel2.Length * Channel3.Length;
            }
        }
    }
}

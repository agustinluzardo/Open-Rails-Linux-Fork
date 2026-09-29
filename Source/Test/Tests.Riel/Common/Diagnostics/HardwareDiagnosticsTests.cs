using System;
using System.Globalization;
using System.IO;

using Riel.Common.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Riel.Common.Diagnostics
{
    [TestClass]
    public class HardwareDiagnosticsTests
    {
        [TestMethod]
        public void GraphicsApiIsDeterminedByTheActiveRenderer()
        {
            Assert.AreEqual("n/a", SystemInfo.DescribeGraphicsApi(null));
#if RIEL_DESKTOPGL
            Assert.AreEqual("OpenGL", SystemInfo.DescribeGraphicsApi("llvmpipe (LLVM 19.1.0)"));
            Assert.AreEqual("OpenGL over Vulkan (Zink)",
                SystemInfo.DescribeGraphicsApi("zink Vulkan 1.3 (NVIDIA GeForce GTX 1070)"));
#else
            Assert.AreEqual("Direct3D 11", SystemInfo.DescribeGraphicsApi("NVIDIA GeForce GTX 1070"));
#endif
        }

        [TestMethod]
        public void CpuUsesMeasuredDieRatherThanAmdControlTemperature()
        {
            using SensorFixture sensors = new SensorFixture();
            sensors.Add("k10temp", ("Tctl", 80000), ("Tdie", 60500));

            Assert.AreEqual(60.5.ToString("0.#", CultureInfo.CurrentCulture) + " °C",
                HardwareTemperatures.Sample(null, sensors.Root, () => null).Cpu);
        }

        [TestMethod]
        public void CpuLabelsControlTemperatureWhenDieIsUnavailable()
        {
            using SensorFixture sensors = new SensorFixture();
            sensors.Add("k10temp", ("Tctl", 80000));

            Assert.AreEqual("80 °C (Tctl)", HardwareTemperatures.Sample(null, sensors.Root, () => null).Cpu);
        }

        [TestMethod]
        public void IntelPackageTakesPrecedenceOverIndividualCores()
        {
            using SensorFixture sensors = new SensorFixture();
            sensors.Add("coretemp", ("Core 0", 88000), ("Package id 0", 66000));

            Assert.AreEqual("66 °C", HardwareTemperatures.Sample(null, sensors.Root, () => null).Cpu);
        }

        [TestMethod]
        public void AmdGpuUsesEdgeAndRejectsAmbiguousCards()
        {
            using SensorFixture sensors = new SensorFixture();
            sensors.Add("amdgpu", ("junction", 92000), ("edge", 52000));
            Func<string> noNvidia = () => throw new InvalidOperationException("Unexpected nvidia-smi call");

            Assert.AreEqual("52 °C", HardwareTemperatures.Sample("zink (AMD Radeon RX 6600)", sensors.Root, noNvidia).Gpu);
            sensors.Add("amdgpu", ("edge", 44000));
            Assert.AreEqual("n/a", HardwareTemperatures.Sample("AMD Radeon RX 6600", sensors.Root, noNvidia).Gpu);
        }

        [TestMethod]
        public void NvidiaGpuMatchesRendererAndRejectsAmbiguousMatches()
        {
            using SensorFixture sensors = new SensorFixture();
            const string output = "NVIDIA GeForce GTX 1070, 47\nNVIDIA GeForce RTX 4080, 62\n";

            Assert.AreEqual("47 °C", HardwareTemperatures.Sample(
                "zink Vulkan 1.3 (GeForce GTX 1070)", sensors.Root, () => output).Gpu);
            Assert.AreEqual("47 °C", HardwareTemperatures.Sample(
                "GeForce GTX 1070/PCIe/SSE2", sensors.Root, () => output).Gpu);
            Assert.AreEqual("n/a", HardwareTemperatures.Sample(
                "zink Vulkan 1.3 (unknown device)", sensors.Root, () => output).Gpu);
            Assert.IsNull(HardwareTemperatures.ParseNvidiaTemperatures(
                "NVIDIA GeForce GTX 1070, 47\nNVIDIA GeForce GTX 1070, 62\n", "NVIDIA GeForce GTX 1070"));
        }

        [TestMethod]
        public void UnidentifiedRendererDoesNotClaimTheTemperatureOfAnotherGpu()
        {
            using SensorFixture sensors = new SensorFixture();
            sensors.Add("amdgpu", ("edge", 52000));
            Assert.AreEqual("n/a", HardwareTemperatures.Sample(null, sensors.Root,
                () => "NVIDIA GeForce GTX 1070, 47").Gpu);
            Assert.AreEqual("n/a", HardwareTemperatures.Sample("zink (unknown device)", sensors.Root,
                () => "NVIDIA GeForce GTX 1070, 47").Gpu);
        }

        private sealed class SensorFixture : IDisposable
        {
            private int index;
            public string Root { get; } = Path.Combine(Path.GetTempPath(), "riel-hwmon-" + Guid.NewGuid().ToString("N"));

            public void Add(string name, params (string Label, long MilliCelsius)[] temperatures)
            {
                string path = Path.Combine(Root, "hwmon" + index++);
                Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, "name"), name);
                for (int i = 0; i < temperatures.Length; i++)
                {
                    string stem = Path.Combine(path, "temp" + (i + 1));
                    File.WriteAllText(stem + "_label", temperatures[i].Label);
                    File.WriteAllText(stem + "_input", temperatures[i].MilliCelsius.ToString(CultureInfo.InvariantCulture));
                }
            }

            public void Dispose()
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
        }
    }
}

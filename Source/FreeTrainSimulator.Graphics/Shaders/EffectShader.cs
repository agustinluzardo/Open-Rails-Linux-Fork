using System;
using System.Diagnostics;
using System.IO;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Native;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.Shaders
{
    public abstract class EffectShader : Effect
    {
        private readonly byte worldIndex = byte.MaxValue;
        private readonly byte wvpIndex = byte.MaxValue;

        // A shader that declares a matrix but never reads it has no such parameter once compiled
        // for OpenGL: the GLSL compiler drops unused uniforms, where Direct3D keeps them in the
        // constant buffer. The popup windows' shader is one - it has no World - so setting a
        // matrix the shader lacks does nothing rather than index past the parameters.
#pragma warning disable CA1044 // Properties should not be write only
        public Matrix World
        {
            set
            {
                if (worldIndex != byte.MaxValue)
                    Parameters[worldIndex].SetValue(value);
            }
        }

        public Matrix WorldViewProjection
        {
            set
            {
                if (wvpIndex != byte.MaxValue)
                    Parameters[wvpIndex].SetValue(value);
            }
        }
#pragma warning restore CA1044 // Properties should not be write only

        protected EffectShader(GraphicsDevice graphicsDevice, string shaderName) :
            base(graphicsDevice, GetEffectCode(shaderName + "Shader"))
        {
            for (byte i = 0; i < Parameters.Count; i++)
            {
                if (Parameters[i].Name.Equals("World", StringComparison.OrdinalIgnoreCase))
                    worldIndex = i;
                if (Parameters[i].Name.Equals("WorldViewProjection", StringComparison.OrdinalIgnoreCase))
                    wvpIndex = i;
            }
        }

        public virtual void SetState() { }

        public virtual void ResetState() { }


        private static byte[] GetEffectCode(string fileName)
        {
            try
            {
                string filePath = Path.Combine(RuntimeInfo.ContentFolder, fileName + ".mgfx");
                return ContentIO.ReadAllBytes(filePath);
            }
            catch (Exception exception)
            {
                Trace.TraceError($"Error while loading effect shader '{fileName}': {exception.Message}");
                throw;
            }
        }

    }
}

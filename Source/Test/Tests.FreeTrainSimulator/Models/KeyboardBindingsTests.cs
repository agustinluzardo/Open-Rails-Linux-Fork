using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Models.Settings;
using MemoryPack;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework.Input;

namespace Tests.FreeTrainSimulator.Models
{
    [TestClass]
    public class KeyboardBindingsTests
    {
        [TestMethod]
        public void RebindingPreservesModifiersThroughSerialization()
        {
            ProfileKeyboardSettingsModel settings = new ProfileKeyboardSettingsModel();
            UserCommandInput input = settings.UserCommands[UserCommand.DisplayHelpWindow];
            input.UniqueDescriptor = UserCommandInput.ComposeUniqueDescriptor(KeyModifiers.Control, 0, Keys.H)
                | (input.UniqueDescriptor & unchecked((int)0xFF000000));
            ProfileKeyboardSettingsModel restored = MemoryPackSerializer.Deserialize<ProfileKeyboardSettingsModel>(MemoryPackSerializer.Serialize(settings));
            Assert.IsTrue(restored.UserCommands[UserCommand.DisplayHelpWindow].IsKeyDown(new KeyboardState(Keys.H, Keys.LeftControl)));
            Assert.IsTrue(restored.UserCommands[UserCommand.DisplayHelpWindow].IsKeyDown(new KeyboardState(Keys.H, Keys.RightControl, Keys.RightShift)));
            Assert.IsFalse(restored.UserCommands[UserCommand.DisplayHelpWindow].IsKeyDown(new KeyboardState(Keys.H)));
        }

        [TestMethod]
        public void QuitDefaultsAcceptEitherAltKey()
        {
            UserCommandInput quit = new ProfileKeyboardSettingsModel().UserCommands[UserCommand.GameQuit];
            Assert.IsTrue(quit.IsKeyDown(new KeyboardState(Keys.F4, Keys.LeftAlt)));
            Assert.IsTrue(quit.IsKeyDown(new KeyboardState(Keys.F4, Keys.RightAlt)));
            Assert.IsFalse(quit.IsKeyDown(new KeyboardState(Keys.F4)));
        }
    }
}

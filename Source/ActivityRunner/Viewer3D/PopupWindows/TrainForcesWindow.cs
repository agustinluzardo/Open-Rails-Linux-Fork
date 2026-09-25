using System.Collections.Generic;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls;
using FreeTrainSimulator.Graphics.Window.Controls.Layout;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    /// <summary>
    /// Compact per-car force monitor inspired by Open Rails' Train Forces window.
    /// Riel already calculates and exposes these values through TrainCar.ForceInfo, so this
    /// view stays on the simulator's canonical physics data instead of duplicating calculations.
    /// </summary>
    internal sealed class TrainForcesWindow : WindowBase
    {
        private readonly List<ForceRow> rows = new List<ForceRow>();
        private Train lastTrain;
        private int lastCarCount;

        private sealed record ForceRow(
            TrainCar Car,
            Label Total,
            Label Motive,
            Label Brake,
            Label Coupler,
            Label Slack,
            Label Derail);

        public TrainForcesWindow(WindowManager owner, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Train Forces"),
                relativeLocation, new Point(690, 260), catalog)
        {
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            rows.Clear();

            Train train = Simulator.Instance.PlayerLocomotive?.Train;
            if (train == null)
            {
                layout.Add(new Label(this, layout.RemainingWidth, Owner.TextFontDefault.Height,
                    Catalog.GetString("No player train."), HorizontalAlignment.Center));
                return layout;
            }

            const int carWidth = 115;
            const int forceWidth = 88;
            const int slackWidth = 72;
            const int derailWidth = 64;

            ControlLayout header = layout.AddLayoutHorizontalLineOfText();
            header.Add(new Label(this, carWidth, header.RemainingHeight, Catalog.GetString("Car")));
            header.Add(new Label(this, forceWidth, header.RemainingHeight, Catalog.GetString("Total"), HorizontalAlignment.Right));
            header.Add(new Label(this, forceWidth, header.RemainingHeight, Catalog.GetString("Motive"), HorizontalAlignment.Right));
            header.Add(new Label(this, forceWidth, header.RemainingHeight, Catalog.GetString("Brake"), HorizontalAlignment.Right));
            header.Add(new Label(this, forceWidth, header.RemainingHeight, Catalog.GetString("Coupler"), HorizontalAlignment.Right));
            header.Add(new Label(this, slackWidth, header.RemainingHeight, Catalog.GetString("Slack"), HorizontalAlignment.Right));
            header.Add(new Label(this, derailWidth, header.RemainingHeight, Catalog.GetString("Derail"), HorizontalAlignment.Right));
            layout.AddHorizontalSeparator();

            ControlLayout scroll = layout.AddLayoutScrollboxVertical(layout.RemainingWidth);
            foreach (TrainCar car in train.Cars)
            {
                ControlLayout line = scroll.AddLayoutHorizontalLineOfText();
                line.Add(new Label(this, carWidth, line.RemainingHeight, car.CarID));
                line.Add(Label total = new Label(this, forceWidth, line.RemainingHeight, null, HorizontalAlignment.Right));
                line.Add(Label motive = new Label(this, forceWidth, line.RemainingHeight, null, HorizontalAlignment.Right));
                line.Add(Label brake = new Label(this, forceWidth, line.RemainingHeight, null, HorizontalAlignment.Right));
                line.Add(Label coupler = new Label(this, forceWidth, line.RemainingHeight, null, HorizontalAlignment.Right));
                line.Add(Label slack = new Label(this, slackWidth, line.RemainingHeight, null, HorizontalAlignment.Right));
                line.Add(Label derail = new Label(this, derailWidth, line.RemainingHeight, null, HorizontalAlignment.Right));
                rows.Add(new ForceRow(car, total, motive, brake, coupler, slack, derail));
            }

            lastTrain = train;
            lastCarCount = train.Cars.Count;
            UpdateRows();
            return layout;
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            base.Update(gameTime, shouldUpdate);
            if (!shouldUpdate)
                return;

            Train train = Simulator.Instance.PlayerLocomotive?.Train;
            if (train != lastTrain || (train?.Cars.Count ?? 0) != lastCarCount)
            {
                Layout();
                return;
            }

            UpdateRows();
        }

        private void UpdateRows()
        {
            foreach (ForceRow row in rows)
            {
                InformationDictionary info = row.Car.ForceInfo.DetailInfo;
                row.Total.Text = info["Total"] ?? "—";
                row.Motive.Text = info["Motive"] ?? "—";
                row.Brake.Text = info["Brake"] ?? "—";
                row.Coupler.Text = info["Coupler"] ?? "—";
                row.Slack.Text = info["Slack"] ?? "—";
                row.Derail.Text = info["DerailCoefficient"] ?? "—";

                row.Derail.TextColor = row.Car.DerailExpected
                    ? Color.Red
                    : row.Car.DerailPossible
                        ? Color.Yellow
                        : Color.White;
            }
        }
    }
}

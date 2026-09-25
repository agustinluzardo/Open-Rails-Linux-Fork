using System.Collections.Generic;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics;
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
    /// Dedicated per-car operations list, matching the role of Open Rails' F9 window.
    /// The actual controls are provided by Riel's existing CarOperationsWindow so command
    /// semantics stay in one place.
    /// </summary>
    internal sealed class TrainCarOperationsWindow : WindowBase
    {
        private readonly WindowManager<ViewerWindowType> windowManager;
        private Train lastTrain;
        private int lastCarCount;

        public TrainCarOperationsWindow(WindowManager owner, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Train Car Operations"),
                relativeLocation, new Point(520, 300), catalog)
        {
            windowManager = owner as WindowManager<ViewerWindowType>;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);

            Train train = Simulator.Instance.PlayerLocomotive?.Train;
            if (train == null)
            {
                layout.Add(new Label(this, layout.RemainingWidth, Owner.TextFontDefault.Height,
                    Catalog.GetString("No player train."), HorizontalAlignment.Center));
                return layout;
            }

            const int idWidth = 150;
            const int typeWidth = 110;
            const int statusWidth = 190;

            ControlLayout header = layout.AddLayoutHorizontalLineOfText();
            header.Add(new Label(this, idWidth, header.RemainingHeight, Catalog.GetString("Car")));
            header.Add(new Label(this, typeWidth, header.RemainingHeight, Catalog.GetString("Type")));
            header.Add(new Label(this, statusWidth, header.RemainingHeight, Catalog.GetString("Status")));
            header.Add(new Label(this, header.RemainingWidth, header.RemainingHeight, Catalog.GetString("Controls"), HorizontalAlignment.Right));
            layout.AddHorizontalSeparator();

            ControlLayout scroll = layout.AddLayoutScrollboxVertical(layout.RemainingWidth);
            foreach (TrainCar car in train.Cars)
            {
                ControlLayout line = scroll.AddLayoutHorizontalLineOfText();
                Color color = car == Simulator.Instance.PlayerLocomotive ? Color.LightGreen :
                    car.BrakesStuck || car is MSTSLocomotive locomotive && locomotive.PowerReduction > 0
                        ? Color.Yellow
                        : Color.White;

                Label id = new Label(this, idWidth, line.RemainingHeight, car.CarID) { Tag = car, TextColor = color };
                Label type = new Label(this, typeWidth, line.RemainingHeight, CarType(car)) { Tag = car, TextColor = color };
                Label status = new Label(this, statusWidth, line.RemainingHeight, CarStatus(car)) { Tag = car, TextColor = color };
                Label controls = new Label(this, line.RemainingWidth, line.RemainingHeight, Catalog.GetString("Open"), HorizontalAlignment.Right)
                    { Tag = car, TextColor = Color.LightSkyBlue };

                id.OnClick += Car_OnClick;
                type.OnClick += Car_OnClick;
                status.OnClick += Car_OnClick;
                controls.OnClick += Car_OnClick;
                line.Add(id);
                line.Add(type);
                line.Add(status);
                line.Add(controls);
            }

            lastTrain = train;
            lastCarCount = train.Cars.Count;
            return layout;
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            base.Update(gameTime, shouldUpdate);
            if (!shouldUpdate)
                return;

            Train train = Simulator.Instance.PlayerLocomotive?.Train;
            if (train != lastTrain || (train?.Cars.Count ?? 0) != lastCarCount)
                Layout();
        }

        private void Car_OnClick(object sender, MouseClickEventArgs e)
        {
            if (sender is not Label { Tag: TrainCar car })
                return;

            Point anchor = new Point(Borders.Right, Borders.Top + Owner.TextFontDefault.Height * 2);
            (windowManager[ViewerWindowType.CarOperationsWindow] as CarOperationsWindow)
                ?.OpenAt(anchor, openAbove: false, car);
        }

        private string CarType(TrainCar car) => car switch
        {
            MSTSElectricLocomotive => Catalog.GetString("Electric"),
            MSTSDieselLocomotive => Catalog.GetString("Diesel"),
            MSTSSteamLocomotive => Catalog.GetString("Steam"),
            MSTSLocomotive => Catalog.GetString("Locomotive"),
            MSTSWagon wagon when wagon.WagonType == WagonType.Passenger => Catalog.GetString("Passenger"),
            MSTSWagon wagon when wagon.WagonType == WagonType.Freight => Catalog.GetString("Freight"),
            _ => Catalog.GetString("Wagon"),
        };

        private string CarStatus(TrainCar car)
        {
            var parts = new List<string>();
            if (car.BrakesStuck)
                parts.Add(Catalog.GetString("Brakes stuck"));
            if (car is MSTSLocomotive locomotive)
            {
                parts.Add(locomotive.LocomotivePowerSupply?.MainPowerSupplyOn == true
                    ? Catalog.GetString("Power on")
                    : Catalog.GetString("Power off"));
                if (locomotive.PowerReduction > 0)
                    parts.Add(Catalog.GetString("Power reduced"));
            }

            if (car is MSTSWagon wagon)
            {
                if (wagon.BrakeSystem.HandbrakePercent > 0)
                    parts.Add(Catalog.GetString("Handbrake"));
                if (!wagon.BrakeSystem.FrontBrakeHoseConnected)
                    parts.Add(Catalog.GetString("Hose disconnected"));
            }

            return parts.Count == 0 ? Catalog.GetString("OK") : string.Join(" · ", parts);
        }

        public override bool Close()
        {
            windowManager[ViewerWindowType.CarOperationsWindow].Close();
            return base.Close();
        }
    }
}

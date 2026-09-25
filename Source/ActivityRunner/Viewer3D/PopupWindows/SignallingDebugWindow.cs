using System;
using System.Collections.Generic;
using System.Linq;

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
    /// Native signalling diagnostic window for the TrackTraveller-based simulator.
    /// Open Rails' original signalling overlay is tightly coupled to the legacy Traveller;
    /// this view uses Train.GetTrainInfo(), the same canonical route data as Track Monitor.
    /// </summary>
    internal sealed class SignallingDebugWindow : WindowBase
    {
        private readonly List<DebugRow> rows = new List<DebugRow>();
        private string lastSignature = string.Empty;

        private sealed record DebugRow(Label Distance, Label Direction, Label Type, Label State, Label Limit);

        public SignallingDebugWindow(WindowManager owner, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Signalling Debug"),
                relativeLocation, new Point(640, 340), catalog)
        {
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            rows.Clear();

            const int directionWidth = 72;
            const int typeWidth = 120;
            const int stateWidth = 210;
            const int limitWidth = 95;

            ControlLayout header = layout.AddLayoutHorizontalLineOfText();
            header.Add(new Label(this, directionWidth, header.RemainingHeight, Catalog.GetString("Direction")));
            header.Add(new Label(this, typeWidth, header.RemainingHeight, Catalog.GetString("Object")));
            header.Add(new Label(this, stateWidth, header.RemainingHeight, Catalog.GetString("State")));
            header.Add(new Label(this, limitWidth, header.RemainingHeight, Catalog.GetString("Limit"), HorizontalAlignment.Right));
            header.Add(new Label(this, header.RemainingWidth, header.RemainingHeight, Catalog.GetString("Distance"), HorizontalAlignment.Right));
            layout.AddHorizontalSeparator();

            ControlLayout scroll = layout.AddLayoutScrollboxVertical(layout.RemainingWidth);
            foreach ((Direction direction, TrainPathItem item) in CurrentItems())
            {
                ControlLayout line = scroll.AddLayoutHorizontalLineOfText();
                Label directionLabel = new Label(this, directionWidth, line.RemainingHeight, DirectionText(direction));
                Label typeLabel = new Label(this, typeWidth, line.RemainingHeight, TypeText(item));
                Label stateLabel = new Label(this, stateWidth, line.RemainingHeight, StateText(item));
                Label limitLabel = new Label(this, limitWidth, line.RemainingHeight, LimitText(item), HorizontalAlignment.Right);
                Label distanceLabel = new Label(this, line.RemainingWidth, line.RemainingHeight, DistanceText(item), HorizontalAlignment.Right);
                line.Add(directionLabel);
                line.Add(typeLabel);
                line.Add(stateLabel);
                line.Add(limitLabel);
                line.Add(distanceLabel);
                rows.Add(new DebugRow(distanceLabel, directionLabel, typeLabel, stateLabel, limitLabel));
            }

            lastSignature = Signature();
            return layout;
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            base.Update(gameTime, shouldUpdate);
            if (!shouldUpdate)
                return;

            string signature = Signature();
            if (!string.Equals(signature, lastSignature, StringComparison.Ordinal))
            {
                Layout();
                return;
            }

            int index = 0;
            foreach ((Direction direction, TrainPathItem item) in CurrentItems())
            {
                if (index >= rows.Count)
                {
                    Layout();
                    return;
                }

                DebugRow row = rows[index++];
                row.Direction.Text = DirectionText(direction);
                row.Type.Text = TypeText(item);
                row.State.Text = StateText(item);
                row.Limit.Text = LimitText(item);
                row.Distance.Text = DistanceText(item);
                row.State.TextColor = StateColor(item);
            }
        }

        private static IEnumerable<(Direction Direction, TrainPathItem Item)> CurrentItems()
        {
            Train train = Simulator.Instance.PlayerLocomotive?.Train;
            if (train == null)
                yield break;

            TrainInfo info = train.GetTrainInfo();
            foreach (TrainPathItem item in Relevant(info.ObjectInfoForward))
                yield return (Direction.Forward, item);
            foreach (TrainPathItem item in Relevant(info.ObjectInfoBackward))
                yield return (Direction.Backward, item);
        }

        private static IEnumerable<TrainPathItem> Relevant(IEnumerable<TrainPathItem> items)
        {
            return (items ?? Enumerable.Empty<TrainPathItem>())
                .Where(item => item.ItemType is TrainPathItemType.Signal
                    or TrainPathItemType.FacingSwitch
                    or TrainPathItemType.Authority
                    or TrainPathItemType.Reversal
                    or TrainPathItemType.WaitingPoint)
                .OrderBy(item => item.DistanceToTrainM);
        }

        private static string Signature()
        {
            return string.Join("|", CurrentItems().Select(entry =>
                $"{(int)entry.Direction}:{(int)entry.Item.ItemType}:{Math.Round(entry.Item.DistanceToTrainM)}"));
        }

        private string DirectionText(Direction direction) =>
            direction == Direction.Forward ? Catalog.GetString("Forward") : Catalog.GetString("Backward");

        private string TypeText(TrainPathItem item) => item.ItemType switch
        {
            TrainPathItemType.Signal => Catalog.GetString("Signal"),
            TrainPathItemType.FacingSwitch => Catalog.GetString("Switch"),
            TrainPathItemType.Authority => Catalog.GetString("Authority"),
            TrainPathItemType.Reversal => Catalog.GetString("Reversal"),
            TrainPathItemType.WaitingPoint => Catalog.GetString("Waiting point"),
            _ => item.ItemType.ToString(),
        };

        private string StateText(TrainPathItem item) => item.ItemType switch
        {
            TrainPathItemType.Signal => item.SignalState.ToString(),
            TrainPathItemType.FacingSwitch => item.SwitchDivertsRight
                ? Catalog.GetString("Diverging right")
                : Catalog.GetString("Straight / left"),
            TrainPathItemType.Authority => item.AuthorityType.GetLocalizedDescription(),
            TrainPathItemType.Reversal => item.Enabled
                ? (item.Valid ? Catalog.GetString("Enabled") : Catalog.GetString("Invalid"))
                : Catalog.GetString("Disabled"),
            TrainPathItemType.WaitingPoint => item.Enabled ? Catalog.GetString("Enabled") : Catalog.GetString("Disabled"),
            _ => string.Empty,
        };

        private string LimitText(TrainPathItem item)
        {
            return item.ItemType == TrainPathItemType.Signal && item.AllowedSpeedMpS > 0 && item.AllowedSpeedMpS < 200
                ? FormatStrings.FormatSpeedLimit(item.AllowedSpeedMpS, Simulator.Instance.RouteModel.MetricUnits)
                : "—";
        }

        private string DistanceText(TrainPathItem item) =>
            FormatStrings.FormatDistance(item.DistanceToTrainM, Simulator.Instance.MetricUnits);

        private static Color StateColor(TrainPathItem item)
        {
            if (item.ItemType == TrainPathItemType.Signal)
            {
                string state = item.SignalState.ToString();
                if (state.Contains("STOP", StringComparison.OrdinalIgnoreCase))
                    return Color.Red;
                if (state.Contains("APPROACH", StringComparison.OrdinalIgnoreCase)
                    || state.Contains("RESTRICT", StringComparison.OrdinalIgnoreCase))
                    return Color.Yellow;
                return Color.LightGreen;
            }

            if (item.ItemType == TrainPathItemType.Authority)
                return item.AuthorityType is EndAuthorityType.EndOfAuthority or EndAuthorityType.EndOfPath or EndAuthorityType.EndOfTrack
                    ? Color.Yellow
                    : Color.White;

            return Color.White;
        }
    }
}

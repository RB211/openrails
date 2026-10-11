// TEMP(linux-port): stand-in for the WinForms MapViewer while it is ported to Avalonia. Never shown.
using System;
using Orts.Simulation.Physics;

namespace Orts.Viewer3D.Debugging
{
    public class MapViewer : IDisposable
    {
        public MapViewer(Orts.Simulation.Simulator simulator, Viewer viewer) { }
        public bool Enabled => false;
        public SwitchWidget switchPickedItem;
        public SignalWidget signalPickedItem;
        public bool ClickedTrain;
        public Train PickedTrain;
        public void Hide() { }
        public void Dispose() { }
    }

    public class SoundDebugForm : IDisposable
    {
        public SoundDebugForm(Viewer viewer) { }
        public void Hide() { }
        public void Dispose() { }
    }
}

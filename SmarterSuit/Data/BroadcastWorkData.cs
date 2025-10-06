namespace Sisk.SmarterSuit.Data {

    public class BroadcastWorkData : Work.Data {

        public BroadcastWorkData(bool state) {
            State = state;
        }

        public bool State { get; }
    }
}
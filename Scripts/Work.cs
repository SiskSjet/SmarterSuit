using System;

namespace Sisk.SmarterSuit {
    public class Work {
        private readonly Action<Data> _action;
        private readonly Data _data;

        public Work(Action<Data> action, Data data = null) {
            _action = action;
            _data = data;
        }

        public string Name => _action.Method.Name;

        public void DoWork() {
            _action(_data);
        }

        public abstract class Data { }
    }
}
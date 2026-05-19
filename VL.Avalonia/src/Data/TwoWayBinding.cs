using System.Reactive.Linq;
using Avalonia;
using VL.Lib.Reactive;

namespace VL.Avalonia.Data
{
    public class TwoWayBinding<TValue, TProperty> : IDisposable
    {
        private readonly AvaloniaObject _control;
        private readonly AvaloniaProperty<TProperty?> _property;

        private IChannel<TValue>? _channel;
        private IDisposable? _subscription;

        private readonly Func<TValue?, TProperty?> _toProperty;
        private readonly Func<TProperty?, TValue?> _toChannel;

        public TwoWayBinding(
            AvaloniaObject control,
            AvaloniaProperty<TProperty?> property,
            Func<TValue?, TProperty?>? toProperty = null,
            Func<TProperty?, TValue?>? toChannel = null
        )
        {
            _control = control;
            _property = property;

            _toProperty =
                toProperty
                ?? (
                    v => v is null ? default : (TProperty?)Convert.ChangeType(v, typeof(TProperty))
                );

            _toChannel =
                toChannel
                ?? (v => v is null ? default! : (TValue)Convert.ChangeType(v, typeof(TValue)));
        }

        public void Bind(IChannel<TValue>? channel)
        {
            if (ReferenceEquals(_channel, channel))
                return;

            _subscription?.Dispose();
            _channel = channel;

            if (channel != null)
            {
                var controlObservable = _control
                    .GetObservable(_property)
                    .Select(_toChannel)
                    // Skip the initial value from the control, trust the channel first
                    .Skip(1);

                _subscription = Observable
                    .Merge(controlObservable, channel)
                    .StartWith(channel.Value)
                    .DistinctUntilChanged()
                    .Subscribe(value =>
                    {
                        // 1. Update VL Channel safely
                        channel.EnsureValue(value);

                        // 2. Update Avalonia cleanly
                        var propertyValue = _toProperty(value);
                        var currentValue = (TProperty?)_control.GetValue(_property);

                        // Round-trip-safe compare: only overwrite if the current
                        // control value, projected back to the channel type, would
                        // differ from the incoming channel value. Avoids clobbering
                        // a high-precision double Slider.Value with a lossy
                        // (double)(float)x round-trip that would trigger a
                        // pointless InvalidateArrange cycle on every drag move.
                        if (!Equals(_toChannel(currentValue), value))
                        {
                            _control.SetCurrentValue(_property, propertyValue);
                        }
                    });
            }
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _channel = null;
        }
    }

    public class TwoWayBinding<T> : TwoWayBinding<T, T>
    {
        public TwoWayBinding(AvaloniaObject control, AvaloniaProperty<T?> property)
            : base(control, property) { }
    }
}

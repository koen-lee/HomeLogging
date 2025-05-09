﻿using System;

namespace TelemetryToRaven
{
    public record EbusMeter : Meter
    {
        private LogItem[] logItems;
        private TimeRange[] _switchTimePeriods;

        public bool ExtendRuns { get; set; }
        public double MinimumFlowTemperature { get; set; }
        public double MaximumFlowTemperature { get; set; }
        public double DesiredModulation { get; set; }
        public LogItem[] LogItems { get => logItems ?? Array.Empty<LogItem>(); set => logItems = value; }
        public string BaseURL { get; set; }
        public bool SwitchThermostat { get; set; }

        public TimeRange[] SwitchTimePeriods { get => _switchTimePeriods ?? Array.Empty<TimeRange>(); set => _switchTimePeriods = value; }
        public double PermanentSwitchTemperature { get; set; } = 5;
        public TimeSpan MinimumOnPeriod { get; set; } = TimeSpan.FromMinutes(45);
        public TimeSpan MinimumOffPeriod { get; set; } = TimeSpan.FromMinutes(45);

        public class LogItem
        {
            public string Path { get; init; }
            public string TimeseriesName { get; init; }
            public string ChildPath { get; init; }
            public string Tag { get; init; }
            public TimeSpan ReadInterval { get; init; }
        }

        public class TimeRange
        {
            /// <summary>
            /// Time of day (utc) to switch on
            /// </summary>
            public TimeSpan On { get; set; }
            /// <summary>
            /// Time of day (utc) to switch off
            /// </summary>
            public TimeSpan Off { get; set; }
        }
    }
}
﻿public enum ItemAddress
{
    OnOff = 1, // 0-Off 1-On 2-toggle
    SpeedMode = 2, // 1-5
    MaxSpeed = 3, // 3,5
    Boost = 6,// 0-Off 1-On 2-toggle
    Timer = 7,// 0-Off 1-On 2-toggle
    TimerSpeed = 8, //0-5
    TimerSetpointMinutes = 9,
    TimerSetpointHours = 10,
    TimerCountdown = 11,
    TimerTemperatureSetpoint = 13,
    BoostSwitchEnabled = 20,
    FireAlarmEnabled = 21,
    RoomTemperatureSetpoint = 24,
    RoomTemperatureSensorSelection = 29,
    RoomTemperatureActual = 30,
    TemperatureOutsideIntake = 31,
    TemperatureInsideExhaust = 32,
    TemperatureInsideIntake = 33,
    TemperatureOutsideExhaust = 34,
    BoostSwitchStatus = 50,
    FireAlarmStatus = 51,
    MinimumFanSpeedSupply = 54,
    MinimumFanSpeedExtract = 55,
    SupplySpeed1 = 58,
    ExtractSpeed1 = 59,
    SupplySpeed2 = 60,
    ExtractSpeed2 = 61,
    SupplySpeed3 = 62,
    ExtractSpeed3 = 63,
    SupplySpeed4 = 64,
    ExtractSpeed4 = 65,
    SupplySpeed5 = 66,
    ExtractSpeed5 = 67,
    DefrostSpeed = 69,

    SupplySpeedBoost = 70,
    ExtractSpeedBoost = 71,
    HeaterEnabled = 96,
    FilterTimerDays = 99,
    FilterCountdown = 100,
    ResetFilterCountdown = 101,
    BoostTurnOnDelay = 102,
    BoostTurnOffDelay = 103,
    TemperatureControlEnabled = 104,
    TemperatureTE5 = 106,
    RTCTime = 111,
    RTCDate = 112,
    WeeklyScheduleEnabled = 114,
    WeeklyScheduleSpeed = 115,
    WeeklyScheduleTemperatureSetpoint = 116,
    ScheduleSetup = 119,
    MotorHours = 126,
}
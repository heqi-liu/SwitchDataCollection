using System;
using System.Collections.Generic;
using System.Linq;
using SwitchDataCollection.Config;
using SwitchDataCollection.LoggerHelper;
using SwitchDataCollection.Model;

namespace SwitchDataCollection.PLCHelper
{
    public interface IPlcCommunicator
    {
        bool Connect();
        bool Disconnect();
        bool IsConnected { get; }
        bool SendData(PlcDataPacket packet);
        PlcDataPacket ReceiveData();
        bool WriteRegister(string registerAddress, object value);
        object ReadRegister(string registerAddress);
        bool SendBatchData(DataBatch batch);
        void ReloadConfig();
    }

    public class PlcCommunicator : IPlcCommunicator, IDisposable
    {
        private string _ipAddress;
        private int _port;
        private bool _enabled;
        private string _startAddress;
        private int _writeWordLength;
        private FinsTcpClient _finsClient;

        public bool IsConnected => _finsClient != null && _finsClient.IsConnected;

        public PlcCommunicator()
        {
            _finsClient = new FinsTcpClient();
            ReloadConfig();
        }

        public void ReloadConfig()
        {
            var config = ConfigManager.GetConfig().PlcCommunication;
            _ipAddress = config.IpAddress;
            _port = config.Port;
            _enabled = config.Enabled;
            _startAddress = config.StartAddress;
            _writeWordLength = config.WriteWordLength;
            Logger.Info($"PLC配置已更新: {_ipAddress}:{_port}, 起始地址: {_startAddress}, 字长: {_writeWordLength}");
        }

        public bool Connect()
        {
            if (!_enabled)
            {
                Logger.Info("PLC通信未启用");
                return false;
            }

            try
            {
                Logger.Info($"连接PLC: {_ipAddress}:{_port}");
                bool result = _finsClient.Connect(_ipAddress, _port);
                Logger.Info(result ? "PLC连接成功" : "PLC连接失败");
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("PLC连接异常", ex);
                return false;
            }
        }

        public bool Disconnect()
        {
            if (!_enabled) return false;

            try
            {
                _finsClient.Disconnect();
                Logger.Info("PLC连接已断开");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("断开PLC连接失败", ex);
                return false;
            }
        }

        public bool SendData(PlcDataPacket packet)
        {
            if (!_enabled || !IsConnected) return false;

            try
            {
                if (packet.ParsedData != null && packet.ParsedData.Count > 0)
                {
                    foreach (var kvp in packet.ParsedData)
                    {
                        WriteRegister(kvp.Key, kvp.Value);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("发送PLC数据失败", ex);
                return false;
            }
        }

        public PlcDataPacket ReceiveData()
        {
            if (!_enabled || !IsConnected) return null;

            try
            {
                return new PlcDataPacket();
            }
            catch (Exception ex)
            {
                Logger.Error("接收PLC数据失败", ex);
                return null;
            }
        }

        public bool WriteRegister(string registerAddress, object value)
        {
            if (!_enabled || !IsConnected) return false;

            try
            {
                if (!ParsePlcAddress(registerAddress, out FinsMemoryArea area, out int address))
                {
                    Logger.Error($"无效地址: {registerAddress}");
                    return false;
                }

                Logger.Info($"写入寄存器: {registerAddress} = {value}");

                string msg;
                if (value is bool boolValue)
                    _finsClient.Write(area, address, boolValue, out msg);
                else if (value is byte byteValue)
                    _finsClient.Write(area, address, byteValue, out msg);
                else if (value is short shortValue)
                    _finsClient.Write(area, address, shortValue, out msg);
                else if (value is ushort ushortValue)
                    _finsClient.Write(area, address, ushortValue, out msg);
                else if (value is int intValue)
                    _finsClient.Write(area, address, intValue, out msg);
                else if (value is uint uintValue)
                    _finsClient.Write(area, address, uintValue, out msg);
                else if (value is float floatValue)
                    _finsClient.Write(area, address, floatValue, out msg);
                else if (value is double doubleValue)
                    _finsClient.Write(area, address, doubleValue, out msg);
                else if (value is string stringValue)
                {
                    _finsClient.WriteString(area, address, (stringValue.Length + 1) / 2 + 1, stringValue);
                    msg = string.Empty;
                }
                else
                {
                    Logger.Error($"不支持的类型: {value.GetType().Name}");
                    return false;
                }

                if (!string.IsNullOrEmpty(msg))
                    Logger.Warning($"写入警告: {msg}");

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"写入寄存器失败: {registerAddress}", ex);
                return false;
            }
        }

        public object ReadRegister(string registerAddress)
        {
            if (!_enabled || !IsConnected) return null;

            try
            {
                if (!ParsePlcAddress(registerAddress, out FinsMemoryArea area, out int address))
                {
                    Logger.Error($"无效地址: {registerAddress}");
                    return null;
                }

                Logger.Info($"读取寄存器: {registerAddress}");
                string msg;
                int value = _finsClient.Read<int>(area, address, out msg);

                if (!string.IsNullOrEmpty(msg))
                    Logger.Warning($"读取警告: {msg}");

                return value;
            }
            catch (Exception ex)
            {
                Logger.Error($"读取寄存器失败: {registerAddress}", ex);
                return null;
            }
        }

        public bool SendBatchData(DataBatch batch)
        {
            if (!_enabled || !IsConnected) return false;

            try
            {
                Logger.Info($"发送批量数据: {batch.TotalRecords} 条");

                if (!ParsePlcAddress(_startAddress, out FinsMemoryArea area, out int baseAddress))
                {
                    Logger.Error($"无效起始地址: {_startAddress}");
                    return false;
                }

                string combinedString = string.Join(",", batch.Records.SelectMany(r => r.Fields.Values.Select(v => v?.ToString() ?? "")));
                
                int charLength = _writeWordLength * 2;
                if (combinedString.Length > charLength)
                {
                    combinedString = combinedString.Substring(0, charLength);
                    Logger.Warning($"数据超长已截断: 原长度:{combinedString.Length + 1}");
                }
                else if (combinedString.Length < charLength)
                {
                    combinedString = combinedString.PadRight(charLength, '\0');
                }
                
                Logger.Info($"写入地址:{area}{baseAddress} 字数:{_writeWordLength} 字符长度:{combinedString.Length}");
                WriteRowAsString(area, baseAddress, combinedString);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("发送批量数据失败", ex);
                return false;
            }
        }

        private bool WriteRowAsString(FinsMemoryArea area, int address, string rowString)
        {
            try
            {
                _finsClient.WriteString(area, address, _writeWordLength, rowString);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"写入字符串失败 地址:{area}{address}", ex);
                return false;
            }
        }

        private bool ParsePlcAddress(string address, out FinsMemoryArea area, out int offset)
        {
            area = FinsMemoryArea.DM;
            offset = 0;

            if (string.IsNullOrEmpty(address)) return false;

            string addr = address.Trim().ToUpper().Replace("_", "");

            if (addr.StartsWith("CIO")) { area = FinsMemoryArea.CIO; addr = addr.Substring(3); }
            else if (addr.StartsWith("W")) { area = FinsMemoryArea.WR; addr = addr.Substring(1); }
            else if (addr.StartsWith("D")) { area = FinsMemoryArea.DM; addr = addr.Substring(1); }
            else if (addr.StartsWith("H")) { area = FinsMemoryArea.HR; addr = addr.Substring(1); }
            else if (addr.StartsWith("T")) { area = FinsMemoryArea.TIM; addr = addr.Substring(1); }
            else if (addr.StartsWith("A")) { area = FinsMemoryArea.AR; addr = addr.Substring(1); }
            else if (addr.StartsWith("C")) { area = FinsMemoryArea.CNT; addr = addr.Substring(1); }
            else if (addr.StartsWith("E0")) { area = FinsMemoryArea.E0; addr = addr.Substring(2); }
            else if (addr.StartsWith("E1")) { area = FinsMemoryArea.E1; addr = addr.Substring(2); }
            else if (addr.StartsWith("E2")) { area = FinsMemoryArea.E2; addr = addr.Substring(2); }
            else if (addr.StartsWith("E3")) { area = FinsMemoryArea.E3; addr = addr.Substring(2); }
            else if (addr.StartsWith("E4")) { area = FinsMemoryArea.E4; addr = addr.Substring(2); }
            else if (addr.StartsWith("E5")) { area = FinsMemoryArea.E5; addr = addr.Substring(2); }
            else if (addr.StartsWith("E6")) { area = FinsMemoryArea.E6; addr = addr.Substring(2); }
            else if (addr.StartsWith("E7")) { area = FinsMemoryArea.E7; addr = addr.Substring(2); }

            return int.TryParse(addr, out offset);
        }

        public void Dispose()
        {
            _finsClient?.Dispose();
        }
    }
}

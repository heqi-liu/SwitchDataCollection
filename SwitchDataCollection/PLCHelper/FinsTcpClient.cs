using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SwitchDataCollection.PLCHelper
{
    public class FinsTcpClient : IDisposable
    {
        #region 私有变量
        private TcpClient _tcpClient;
        private Socket _socket;
        private object _lock = new object();
        private byte _clientNode;
        private byte _plcNode;
        #endregion

        #region 公共属性

        /// <summary>
        /// 记录FINS的发送和接收数据包事件
        /// 第一个参数是发送的数据包，第二个参数是响应的数据包，第三个参数的获取到响应数据包所花费的时间（ms）
        /// </summary>
        public event Action<byte[], byte[], double> RecordDataEvent;

        public bool IsConnected
        {
            get
            {
                if (_tcpClient == null)
                    return false;
                
                if (!_tcpClient.Connected)
                    return false;
                
                try
                {
                    if (_socket != null && _socket.Poll(0, SelectMode.SelectRead))
                    {
                        byte[] buffer = new byte[1];
                        if (_socket.Receive(buffer, SocketFlags.Peek) == 0)
                        {
                            return false;
                        }
                    }
                }
                catch
                {
                    return false;
                }
                
                return true;
            }
        }

        public string PlcIp { get; private set; }
        public int PlcPort { get; private set; }
        #endregion

        #region 连接
        public bool Connect(string ip, int port)
        {
            lock (_lock)
            {
                try
                {
                    Disconnect();
                    PlcIp = ip;
                    PlcPort = port;

                    _tcpClient = new TcpClient();
                    _tcpClient.ReceiveTimeout = 3000;
                    _tcpClient.SendTimeout = 3000;
                    _tcpClient.Connect(ip, port);
                    _socket = _tcpClient.Client;

                    //握手协议 共20个字节【标识头4个字节，长度4个字节，命令码4个字节，错误代码4个字节，客户端节点地址4个字节】
                    byte[] handshakeProtocol = new byte[20];
                    //标识头命令：固定FINS
                    handshakeProtocol[0] = 0x46;//F
                    handshakeProtocol[1] = 0x49;//I
                    handshakeProtocol[2] = 0x4E;//N
                    handshakeProtocol[3] = 0x53;//S
                                                //数据长度：4个字节
                    handshakeProtocol[4] = 0;
                    handshakeProtocol[5] = 0;
                    handshakeProtocol[6] = 0;
                    handshakeProtocol[7] = 0x0C;//Length长度:后面跟的字节长度:12个字节
                                                //00000000：固定命令；【索引 8~11】
                                                //00000000：错误代码；【索引 12~15】
                    handshakeProtocol[16] = 0;
                    handshakeProtocol[17] = 0;
                    handshakeProtocol[18] = 0;
                    handshakeProtocol[19] = (byte)0;//FINS Frame (工控机IP节点的最后一个字节，如 192.168.1.139 就填入139),frame为0时服务端为客户端自动分配IP尾号
                    _socket.Send(handshakeProtocol);

                    byte[] feedbackBuffer = new byte[24];//反馈结果
                    _socket.Receive(feedbackBuffer, SocketFlags.None);

                    if (handshakeProtocol[0] == feedbackBuffer[0] && handshakeProtocol[1] == feedbackBuffer[1] && handshakeProtocol[2] == feedbackBuffer[2] && handshakeProtocol[3] == feedbackBuffer[3]
                   && feedbackBuffer[7] == 0x10 && feedbackBuffer[15] == 0x00 && (handshakeProtocol[19] == 0 || feedbackBuffer[19] == handshakeProtocol[19]))
                    {

                        _clientNode = feedbackBuffer[19];
                        _plcNode = feedbackBuffer[23];
                    }

                    return _tcpClient.Connected;
                }
                catch
                {
                    return false;
                }
            }
        }

        public void Disconnect()
        {
            lock (_lock)
            {
                if (_socket != null) _socket.Close();
                if (_tcpClient != null) _tcpClient.Close();
                _tcpClient = null;
            }
        }

        #endregion

        #region 通用读写
        public T Read<T>(FinsMemoryArea area, int startAddress, out string msg) where T : struct
        {
            Type type = typeof(T);
            bool isBit = type == typeof(bool);
            int byteCount = 0;

            if (type == typeof(bool)) byteCount = 1;
            else if (type == typeof(byte) || type == typeof(sbyte)) byteCount = 1;
            else if (type == typeof(short) || type == typeof(ushort)) byteCount = 2;
            else if (type == typeof(int) || type == typeof(uint) || type == typeof(float)) byteCount = 4;
            else if (type == typeof(long) || type == typeof(ulong) || type == typeof(double)) byteCount = 8;
            else msg = "不支持的类型";

            byte[] rcvBuffer = new byte[0];
            int errorCode = SendCommand(area, startAddress, (byteCount + 1) / 2, isBit, true, ref rcvBuffer, out msg);

            if (byteCount > 1)
                Array.Reverse(rcvBuffer);

            if (type == typeof(bool))
                return (T)(object)(rcvBuffer[0] != 0);
            if (type == typeof(byte))
                return (T)(object)rcvBuffer[0];
            if (type == typeof(sbyte))
                return (T)(object)(sbyte)rcvBuffer[0];
            if (type == typeof(short))
                return (T)(object)BitConverter.ToInt16(rcvBuffer, 0);
            if (type == typeof(ushort))
                return (T)(object)BitConverter.ToUInt16(rcvBuffer, 0);
            if (type == typeof(int))
                return (T)(object)BitConverter.ToInt32(rcvBuffer, 0);
            if (type == typeof(uint))
                return (T)(object)BitConverter.ToUInt32(rcvBuffer, 0);
            if (type == typeof(float))
                return (T)(object)BitConverter.ToSingle(rcvBuffer, 0);
            if (type == typeof(double))
                return (T)(object)BitConverter.ToDouble(rcvBuffer, 0);
            if (type == typeof(long))
                return (T)(object)BitConverter.ToInt64(rcvBuffer, 0);
            if (type == typeof(ulong))
                return (T)(object)BitConverter.ToUInt64(rcvBuffer, 0);

            return default(T);
        }

        public void Write<T>(FinsMemoryArea area, int startAddress, T value, out string msg) where T : struct
        {
            Type type = typeof(T);
            bool isBit = type == typeof(bool);
            byte[] datas = new byte[0];

            if (type == typeof(bool))
            {
                datas = BitConverter.GetBytes(Convert.ToBoolean(value));
            }
            else if (type == typeof(sbyte))
            {
                datas = new byte[1] { (byte)Convert.ToSByte(value) };
            }
            else if (type == typeof(byte))
            {
                datas = new byte[1] { Convert.ToByte(value) };
            }
            else if (type == typeof(short))
            {
                datas = BitConverter.GetBytes(Convert.ToInt16(value));
            }
            else if (type == typeof(ushort))
            {
                datas = BitConverter.GetBytes(Convert.ToUInt16(value));
            }
            else if (type == typeof(int))
            {
                datas = BitConverter.GetBytes(Convert.ToInt32(value));
            }
            else if (type == typeof(uint))
            {
                datas = BitConverter.GetBytes(Convert.ToUInt32(value));
            }
            else if (type == typeof(long))
            {
                datas = BitConverter.GetBytes(Convert.ToInt64(value));
            }
            else if (type == typeof(ulong))
            {
                datas = BitConverter.GetBytes(Convert.ToUInt64(value));
            }
            else if (type == typeof(float))
            {
                datas = BitConverter.GetBytes(Convert.ToSingle(value));
            }
            else if (type == typeof(double))
            {
                datas = BitConverter.GetBytes(Convert.ToDouble(value));
            }
            else
            {
                msg = $"写Fins数据暂不支持其他类型：{value.GetType()}";
            }

            if (datas.Length > 1)
                Array.Reverse(datas);

            byte[] rcvBuffer = new byte[0];
            int bitIndexOrWordLength = 0;
            if (!isBit)
            {
                bitIndexOrWordLength = (datas.Length + 1) / 2;
            }

            int err = SendCommand(area, startAddress, bitIndexOrWordLength, isBit, false, ref rcvBuffer, out msg, datas);
            if (err != 0)
            {
                msg = $"写入失败，错误码：{err}";
            }
        }
        #endregion

        #region 字符串
        public string ReadString(FinsMemoryArea area, int startAddress, int wordLength)
        {
            int byteLen = wordLength * 2;
            byte[] buffer = new byte[byteLen];

            SendCommand(area, startAddress, wordLength, false, true, ref buffer, out string msg);

            return Encoding.ASCII.GetString(buffer).TrimEnd('\0');
        }

        public void WriteString(FinsMemoryArea area, int startAddress, int wordLength, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                text = string.Empty;
            }

            text = text.PadRight(wordLength * 2, '\0');

            byte[] asciiBytes = Encoding.ASCII.GetBytes(text);
            int wordCount = (asciiBytes.Length + 1) / 2;

            byte[] sendBuffer = new byte[wordCount * 2];
            Buffer.BlockCopy(asciiBytes, 0, sendBuffer, 0, asciiBytes.Length);

            byte[] buffer = new byte[0];
            int err = SendCommand(area, startAddress, wordCount, false, false, ref buffer, out string msg, sendBuffer);

            if (err != 0)
                throw new Exception($"写入失败，错误码：{err}");
        }
        #endregion

        #region 底层发送
        private int SendCommand(FinsMemoryArea memoryArea, int startAddress, int lenOrBit, bool isBit, bool isRead, ref byte[] receiveData, out string errMsg, byte[] writeData = null)
        {
            errMsg = string.Empty;
            lock (_lock)
            {
                try
                {
                    if (!IsConnected)
                    {
                        errMsg = $"【未建立连接】尚未连接PLC成功或者握手协议失败,请检查配置和网络";
                        return -1;
                    }

                    if (startAddress < 0 || startAddress > 65535)
                    {
                        errMsg = $"【参数非法】欧姆龙PLC的FINS协议的起始地址必须在0~65535之间,当前起始地址【{startAddress}】";
                        return 1002;
                    }

                    if (isRead && lenOrBit > 1000)
                    {
                        errMsg = $"【参数非法】读取的位索引 或者 字长度不能超过1000，当前起始地址【{startAddress}】，读取长度【{lenOrBit}】";
                        return 1003;
                    }

                    byte[] sendBytes = new byte[isRead ? 34 : 34 + writeData.Length];

                    sendBytes[0] = 0x46;//F
                    sendBytes[1] = 0x49;//I
                    sendBytes[2] = 0x4E;//N
                    sendBytes[3] = 0x53;//S

                    uint dataLength = (uint)(26 + (isRead ? 0 : writeData.Length));
                    byte[] lenBytes = BitConverter.GetBytes(dataLength);
                    sendBytes[4] = lenBytes[3];
                    sendBytes[5] = lenBytes[2];
                    sendBytes[6] = lenBytes[1];
                    sendBytes[7] = lenBytes[0];
                    //命令码 固定 00 00 00 02
                    sendBytes[8] = 0;
                    sendBytes[9] = 0;
                    sendBytes[10] = 0;
                    sendBytes[11] = 0x02;

                    sendBytes[16] = 0x80;
                    sendBytes[17] = 0x00;
                    sendBytes[18] = 0x02;
                    sendBytes[19] = 0x00;
                    sendBytes[20] = _plcNode;
                    sendBytes[21] = 0x00;
                    sendBytes[22] = 0x00;
                    sendBytes[23] = _clientNode;
                    sendBytes[24] = 0x00;
                    sendBytes[25] = 0x15;//SID:SID用于标识数据发送的过程。 【服务标识】

                    sendBytes[26] = 0x01;
                    sendBytes[27] = (byte)(isRead ? 0x01 : 0x02);

                    sendBytes[28] = FinsCommandBuilder.GetAreaCode(memoryArea, isBit);

                    byte[] addrBytes = BitConverter.GetBytes((ushort)startAddress);
                    sendBytes[29] = addrBytes[1];
                    sendBytes[30] = addrBytes[0];

                    byte[] lengthParts = BitConverter.GetBytes((ushort)lenOrBit);
                    if (isBit)
                    {
                        sendBytes[31] = lengthParts[0];
                        sendBytes[32] = 0x00;
                        sendBytes[33] = 0x01;
                    }
                    else
                    {
                        sendBytes[31] = 0x00;
                        sendBytes[32] = lengthParts[1];
                        sendBytes[33] = lengthParts[0];
                    }

                    if (!isRead)
                    {
                        Array.Copy(writeData, 0, sendBytes, 34, writeData.Length);
                    }


                    try
                    {
                        _socket.Send(sendBytes, SocketFlags.None);
                    }
                    catch (SocketException ex)
                    {
                        errMsg = $"发送数据时出现套接字异常，【{ex.SocketErrorCode}】,{ex.Message}";
                        return 1004;
                    }
                    catch (Exception ex)
                    {
                        errMsg = $"发送数据时出现异常，【{ex.Message}】";
                        return 1005;
                    }


                    byte[] buffer = new byte[2048];
                    byte[] receiveByts = null;//接收的有效字节流
                    int rcvCount = 0;//收到的字节数

                    System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        rcvCount = _socket.Receive(buffer);
                        receiveByts = new ArraySegment<byte>(buffer, 0, rcvCount).Array;
                        stopwatch.Stop();
                        RecordDataEvent?.Invoke(sendBytes, receiveByts, stopwatch.Elapsed.TotalMilliseconds);

                    }
                    catch (SocketException ex)
                    {
                        errMsg = $"【网络异常】接收数据失败，套接字错误【{ex.SocketErrorCode}】,【{ex.Message}】";
                        return 1006;
                    }
                    catch (Exception ex)
                    {
                        errMsg = $"【处理异常】接收数据失败，【{ex.Message}】";
                        return 1007;
                    }


                    if (receiveByts == null || receiveByts.Length < 30)
                    {
                        errMsg = $"接收数据为空，或者低于30个字节，接收数据为【{(receiveByts == null ? "" : string.Join(",", receiveByts))}】";
                        return 1008;
                    }
                    if (receiveByts[0] != sendBytes[0] || receiveByts[1] != sendBytes[1] || receiveByts[2] != sendBytes[2] || receiveByts[3] != sendBytes[3])
                    {
                        errMsg = $"接收数据头非法，不是从【FINS】开始，接收数据为【{string.Join(",", receiveByts)}】";
                        return 1009;
                    }
                    if (receiveByts[15] != 0)//if (receiveByts[11] != 2 || receiveByts[15] != 0)
                    {
                        errMsg = $"数据非法，解析时出现错误，错误号【{receiveByts[15]}】，接收数据为【{string.Join(",", receiveByts)}】";
                        return 1010;
                    }

                    if (receiveByts[26] != sendBytes[26] || receiveByts[27] != sendBytes[27])
                    {
                        errMsg = $"读写命令不匹配，发送命令【{sendBytes[26]:X2}{sendBytes[27]:X2}】，反馈命令【{receiveByts[26]:X2}{receiveByts[27]:X2}】";
                        return 1012;
                    }
                    if (receiveByts[28] != 0 || receiveByts[29] != 0)
                    {
                        errMsg = FinsConverter.GetErrorMessage(receiveByts[28], receiveByts[29]);
                        return 1013;
                    }


                    if (isRead)
                    {
                        //读取位时，只读取一位
                        receiveData = new byte[isBit ? 1 : (lenOrBit * 2)];
                        Array.Copy(receiveByts, 30, receiveData, 0, receiveData.Length);
                    }

                    return 0;
                }
                catch
                {
                    Disconnect();
                    return -99;
                }
            }
        }
        #endregion

        public void Dispose()
        {
            Disconnect();
        }
    }
}

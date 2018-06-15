﻿using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils;
using YIEternalMIS.Common;

namespace WeightManage.Module.SerialPortUtil
{
    public class ElectronicScale
    {
        #region 称重计算参数
        /// <summary>
        /// 连续相同重量次数
        /// </summary>
        private int _sameCount = 10;
        /// <summary>
        /// 误差范围
        /// </summary>
        private decimal _errorLimit = 0.1M;
        /// <summary>
        /// 计数
        /// </summary>
        private int _readCount = 0;
        /// <summary>
        /// 上一次读数
        /// </summary>
        private decimal _lastWeight = Decimal.Zero;
        /// <summary>
        /// 最小重量起
        /// </summary>
        private decimal _minWeight = 0.2M;

        /// <summary>
        /// 重量队列，先进先出
        /// </summary>
        private Queue<decimal> _dataQueue = new Queue<decimal>();

        /// <summary>
        /// 是否有新的重物
        /// </summary>
        private bool _isChanged = true;
        /// <summary>
        /// 最小起秤重量
        /// </summary>
        public decimal MinWeight
        {
            get
            { return _minWeight; }
            set
            { _minWeight = value; }
        }
        /// <summary>
        /// 频率（连续相同重量次数,5--10）
        /// </summary>
        public int SameCount
        {
            get
            { return _sameCount; }
            set
            {
                _sameCount = value;
            }
        }
        /// <summary>
        /// 误差范围（设置为重物的最小重量）
        /// </summary>
        public decimal ErrorLimit
        {
            get
            {
                return _errorLimit;
            }
            set
            {
                _errorLimit = value;
            }
        }
        #endregion
        #region 构造函数

        /// <summary>
        /// 参数构造函数（使用枚举参数构造）
        /// </summary>
        /// <param name="baud">波特率</param>
        /// <param name="par">奇偶校验位</param>
        /// <param name="sBits">停止位</param>
        /// <param name="dBits">数据位</param>
        /// <param name="name">串口号</param>
        /// <param name="sameCount">连续相同重量次数</param>
        /// <param name="minWeight">最小重量起</param>
        /// <param name="errLimit">误差范围</param>
        public void InitScale(string name, SerialPortBaudRates baud, Parity par, SerialPortDatabits dBits, StopBits sBits,int sameCount,decimal minWeight,decimal errLimit)
        {
            //串口设置
            _portName = name;
            _baudRate = baud;
            _parity = par;
            _dataBits = dBits;
            _stopBits = sBits;

            //计算重量
            _sameCount = sameCount;
            _minWeight = minWeight;
            _errorLimit = errLimit;

            comPort.DataReceived += new SerialDataReceivedEventHandler(comPort_DataReceived);
            comPort.ErrorReceived += new SerialErrorReceivedEventHandler(comPort_ErrorReceived);
        }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public ElectronicScale()
        {
            _portName = "COM1";
            _baudRate = SerialPortBaudRates.BaudRate_9600;
            _parity = Parity.None;
            _dataBits = SerialPortDatabits.EightBits;
            _stopBits = StopBits.One;

            comPort.DataReceived += new SerialDataReceivedEventHandler(comPort_DataReceived);
            comPort.ErrorReceived += new SerialErrorReceivedEventHandler(comPort_ErrorReceived);
        }

        #endregion
        #region 串口相关参数
        /// <summary>
        /// 接收事件是否有效 false表示有效
        /// </summary>
        public bool ReceiveEventFlag = false;
        /// <summary>
        /// 结束符比特
        /// </summary>
        public byte EndByte = 0x23;//string End = "#";
        /// <summary>
        /// 准备关闭
        /// </summary>
        public bool ReadyClose = false;
        /// <summary>
        /// 数据接受中
        /// </summary>
        public bool InReceive = false;
        /// <summary>
        /// 完整协议的记录处理事件
        /// </summary>
        public event WeightReceivedEventHandler DataReceived;
        public event SerialErrorReceivedEventHandler Error;

        #region 变量属性
        private string _portName = "COM1";//串口号，默认COM1
        private SerialPortBaudRates _baudRate = SerialPortBaudRates.BaudRate_57600;//波特率
        private Parity _parity = Parity.None;//校验位
        private StopBits _stopBits = StopBits.One;//停止位
        private SerialPortDatabits _dataBits = SerialPortDatabits.EightBits;//数据位

        private SerialPort comPort = new SerialPort();

        /// <summary>
        /// 串口号
        /// </summary>
        public string PortName
        {
            get { return _portName; }
            set { _portName = value; }
        }

        /// <summary>
        /// 波特率
        /// </summary>
        public SerialPortBaudRates BaudRate
        {
            get { return _baudRate; }
            set { _baudRate = value; }
        }

        /// <summary>
        /// 奇偶校验位
        /// </summary>
        public Parity Parity
        {
            get { return _parity; }
            set { _parity = value; }
        }

        /// <summary>
        /// 数据位
        /// </summary>
        public SerialPortDatabits DataBits
        {
            get { return _dataBits; }
            set { _dataBits = value; }
        }

        /// <summary>
        /// 停止位
        /// </summary>
        public StopBits StopBits
        {
            get { return _stopBits; }
            set { _stopBits = value; }
        }

        #endregion

       

        /// <summary>
        /// 端口是否已经打开
        /// </summary>
        public bool IsOpen
        {
            get
            {
                return comPort.IsOpen;
            }
        }

        /// <summary>
        /// 打开端口
        /// </summary>
        /// <returns></returns>
        public void OpenPort()
        {
            if (comPort.IsOpen) comPort.Close();

            comPort.PortName = _portName;
            comPort.BaudRate = (int)_baudRate;
            comPort.Parity = _parity;
            comPort.DataBits = (int)_dataBits;
            comPort.StopBits = _stopBits;

            comPort.Open();
        }

        /// <summary>
        /// 关闭端口
        /// </summary>
        public void ClosePort()
        {
            if (comPort.IsOpen) comPort.Close();
        }

        /// <summary>
        /// 丢弃来自串行驱动程序的接收和发送缓冲区的数据
        /// </summary>
        public void DiscardBuffer()
        {
            comPort.DiscardInBuffer();
            comPort.DiscardOutBuffer();
        }

        /// <summary>
        /// 数据接收处理
        /// </summary>
        void comPort_DataReceived1(object sender, SerialDataReceivedEventArgs e)
        {
            //禁止接收事件时直接退出
            if (ReceiveEventFlag) return;

            #region 根据结束字节来判断是否全部获取完成
            List<byte> _byteData = new List<byte>();
            bool found = false;//是否检测到结束符号
            while (comPort.BytesToRead > 0 || !found)
            {
                byte[] readBuffer = new byte[comPort.ReadBufferSize + 1];
                int count = comPort.Read(readBuffer, 0, comPort.ReadBufferSize);
                for (int i = 0; i < count; i++)
                {
                    _byteData.Add(readBuffer[i]);

                    if (readBuffer[i] == EndByte)
                    {
                        found = true;
                    }
                }
            }
            #endregion

            //字符转换
            string readString = System.Text.Encoding.Default.GetString(_byteData.ToArray(), 0, _byteData.Count);

            //触发整条记录的处理
            //if (DataReceived != null)
            //{
            //    DataReceived(new WeightReceivedEventArgs(readString));
            //}
        }

        void comPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (ReadyClose)
            {
                return;
            }
            InReceive = true;
            try
            {
                byte firstByte = Convert.ToByte(comPort.ReadByte());
                if (firstByte == 0x02)
                {
                    int bytesRead = comPort.ReadBufferSize;
                    byte[] bytesData = new byte[bytesRead];
                    byte byteData;

                    for (int i = 0; i < bytesRead - 1; i++)
                    {
                        byteData = Convert.ToByte(comPort.ReadByte());
                        if (byteData == 0x03) //结束
                        {
                            break;
                        }
                        bytesData[i] = byteData;
                    }
                    string strtemp = Encoding.Default.GetString(bytesData);
                    WeightDataScale(strtemp);
                }
            }
            catch (Exception exception)
            {
                LogNHelper.Exception(exception);
            }
            finally
            {
                InReceive = false;
            }
        }


        /// <summary>
        /// 错误处理函数
        /// </summary>
        void comPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            if (Error != null)
            {
                Error(sender, e);
            }
            else
            {
                LogNHelper.Exception(e.ToString());
            }
        }

        /// <summary>
        /// 返回串口读取的重量
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        private decimal GetWeightOfPort(string weight)
        {
            // AppNetInfo(weight+"\r");
            if (string.IsNullOrEmpty(weight) || weight.IndexOf("+") < 0 || weight.Length < 6)
            {
                return 0M;
            }
            weight = weight.Replace("+", "");
            weight = int.Parse(weight.Substring(0, 5)).ToString() + "." + weight.Substring(5, 1);
            // AppNetInfo(weight);
            return weight.ToDecimal(2);
        }

        /// <summary>
        /// 计算重量
        /// </summary>
        /// <param name="weight"></param>
        private void WeightDataScale(string weight)
        {
            if (DataReceived == null)
            {
                return;
            }

            decimal newWeight= GetWeightOfPort(weight);
            //DataReceived(new WeightReceivedEventArgs(readString));
            if (string.IsNullOrEmpty(weight))
            {
                return;
            }


            //传进来的重量
            // decimal weight = tempweight;
            if (newWeight <= _minWeight)
            {
                _readCount = 0;
                _isChanged = true;
                return;
            }

            if (Math.Abs(newWeight - _lastWeight) <= _errorLimit)
            {
                _readCount++;
            }

            _lastWeight = newWeight;
            if (_readCount >= _sameCount && _isChanged)
            {
                if (newWeight >= _minWeight)
                {
                    _readCount = 0;
                    _lastWeight = newWeight;
                    _isChanged = false;
                    //更新信号灯状态
                  DataReceived(new WeightReceivedEventArgs(newWeight));
                }

            }

        }
        #region 数据写入操作

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="msg"></param>
        public void WriteData(string msg)
        {
            if (!(comPort.IsOpen)) comPort.Open();

            comPort.Write(msg);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="msg">写入端口的字节数组</param>
        public void WriteData(byte[] msg)
        {
            if (!(comPort.IsOpen)) comPort.Open();

            comPort.Write(msg, 0, msg.Length);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="msg">包含要写入端口的字节数组</param>
        /// <param name="offset">参数从0字节开始的字节偏移量</param>
        /// <param name="count">要写入的字节数</param>
        public void WriteData(byte[] msg, int offset, int count)
        {
            if (!(comPort.IsOpen)) comPort.Open();

            comPort.Write(msg, offset, count);
        }

        /// <summary>
        /// 发送串口命令
        /// </summary>
        /// <param name="SendData">发送数据</param>
        /// <param name="ReceiveData">接收数据</param>
        /// <param name="Overtime">重复次数</param>
        /// <returns></returns>
        public int SendCommand(byte[] SendData, ref byte[] ReceiveData, int Overtime)
        {
            if (!(comPort.IsOpen)) comPort.Open();

            ReceiveEventFlag = true;        //关闭接收事件
            comPort.DiscardInBuffer();      //清空接收缓冲区                 
            comPort.Write(SendData, 0, SendData.Length);

            int num = 0, ret = 0;
            while (num++ < Overtime)
            {
                if (comPort.BytesToRead >= ReceiveData.Length) break;
                System.Threading.Thread.Sleep(1);
            }

            if (comPort.BytesToRead >= ReceiveData.Length)
            {
                ret = comPort.Read(ReceiveData, 0, ReceiveData.Length);
            }

            ReceiveEventFlag = false;       //打开事件
            return ret;
        }

        #endregion

        #region 格式转换
        /// <summary>
        /// 转换十六进制字符串到字节数组
        /// </summary>
        /// <param name="msg">待转换字符串</param>
        /// <returns>字节数组</returns>
        public static byte[] HexToByte(string msg)
        {
            msg = msg.Replace(" ", "");//移除空格

            //create a byte array the length of the
            //divided by 2 (Hex is 2 characters in length)
            byte[] comBuffer = new byte[msg.Length / 2];
            for (int i = 0; i < msg.Length; i += 2)
            {
                //convert each set of 2 characters to a byte and add to the array
                comBuffer[i / 2] = (byte)Convert.ToByte(msg.Substring(i, 2), 16);
            }

            return comBuffer;
        }

        /// <summary>
        /// 转换字节数组到十六进制字符串
        /// </summary>
        /// <param name="comByte">待转换字节数组</param>
        /// <returns>十六进制字符串</returns>
        public static string ByteToHex(byte[] comByte)
        {
            StringBuilder builder = new StringBuilder(comByte.Length * 3);
            foreach (byte data in comByte)
            {
                builder.Append(Convert.ToString(data, 16).PadLeft(2, '0').PadRight(3, ' '));
            }

            return builder.ToString().ToUpper();
        }
        #endregion

        /// <summary>
        /// 检查端口名称是否存在
        /// </summary>
        /// <param name="port_name"></param>
        /// <returns></returns>
        public static bool Exists(string port_name)
        {
            foreach (string port in SerialPort.GetPortNames()) if (port == port_name) return true;
            return false;
        }

        /// <summary>
        /// 格式化端口相关属性
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        public static string Format(SerialPort port)
        {
            return String.Format("{0} ({1},{2},{3},{4},{5})",
                port.PortName, port.BaudRate, port.DataBits, port.StopBits, port.Parity, port.Handshake);
        }
    }

    public class WeightReceivedEventArgs : EventArgs
    {
        public decimal DataReceived;
        public WeightReceivedEventArgs(decimal m_DataReceived)
        {
            this.DataReceived = m_DataReceived;
        }
    }

    public delegate void WeightReceivedEventHandler(WeightReceivedEventArgs e);


    #endregion
}

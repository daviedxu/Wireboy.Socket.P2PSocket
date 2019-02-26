﻿/*
 * 主服务
 * 接收心跳包、客户端与服务器的数据交流
 * 
 */
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using Wireboy.Socket.P2PService.Models;
using System.Collections.Concurrent;
using Wireboy.Socket.P2PService.Services;

namespace Wireboy.Socket.P2PService
{
    public class P2PService
    {
        public TcpClientMapHelper _tcpMapHelper = new TcpClientMapHelper();
        private TaskFactory _taskFactory = new TaskFactory();
        public P2PService()
        {

        }

        public void Start()
        {
            //监听通讯端口
            ListenServerPort();
        }

        /// <summary>
        /// 监听通讯端口
        /// </summary>
        public void ListenServerPort()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, ConfigServer.AppSettings.ServerPort);
            listener.Start();
            while (true)
            {
                TcpClient tcpClient = listener.AcceptTcpClient();
                Logger.Write("数据端口：接收到来自{0}的tcp接入", tcpClient.Client.RemoteEndPoint);
                _taskFactory.StartNew(() =>
                {
                    RecieveClientTcp(tcpClient);
                });
            }
        }

        public void RecieveClientTcp(TcpClient readTcp)
        {
            NetworkStream readStream = readTcp.GetStream();
            TcpResult tcpResult = new TcpResult(readStream, readTcp, ReievedTcpDataCallBack);
            while (readTcp.Connected)
            {
                try
                {
                   int length = readStream.Read(tcpResult.Readbuffer, 0, tcpResult.Readbuffer.Length);
                    DoRecieveClientTcp(tcpResult, length);
                    tcpResult.ResetReadBuffer();
                }catch(Exception ex)
                {
                    Logger.Write("P2PService -> RecieveClientTcp: {0}", ex);
                }
            }
        }
        public void DoRecieveClientTcp(TcpResult tcpResult,int length)
        {
            if (length > 0)
            {
                int curReadIndex = 0;
                do
                {
                    tcpResult.ReadOnePackageData(length, ref curReadIndex);
                } while (curReadIndex <= length - 1);
            }
        }

        public void ReievedTcpDataCallBack(byte[] data, TcpResult tcpResult)
        {
            switch (data[0])
            {
                case (byte)MsgType.心跳包:
                    ; break;
                case (byte)MsgType.身份验证:
                    ; break;
                case (byte)MsgType.本地服务名:
                    {
                        string key = BitConverter.ToString(data, 1);
                        _tcpMapHelper.SetHomeClient(tcpResult.ReadTcp, key);
                        Logger.Write("设置本地服务名 ip:{0} key:{1}", tcpResult.ReadTcp.Client.RemoteEndPoint, key);
                    }
                    break;
                case (byte)MsgType.远程服务名:
                    {
                        string key = BitConverter.ToString(data, 1);
                        _tcpMapHelper.SetControlClient(tcpResult.ReadTcp, key);
                        Logger.Write("设置远程服务名 ip:{0} key:{1}", tcpResult.ReadTcp.Client.RemoteEndPoint, key);
                    }
                    break;
                case (byte)MsgType.数据转发:
                case (byte)MsgType.连接断开:
                    {
                        TcpClient toClient = _tcpMapHelper[tcpResult.ReadTcp];
                        if (toClient != null)
                        {
                            try
                            {
                                toClient.WriteAsync(data, MsgType.数据转发);
                            }
                            catch (Exception ex)
                            {
                                Logger.Write("数据转发异常：{0}", ex);
                                _tcpMapHelper[tcpResult.ReadTcp] = null;
                            }
                        }
                    }
                    break;
            }
        }
    }
}

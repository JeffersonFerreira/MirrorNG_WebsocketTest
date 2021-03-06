#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AOT;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mirror.Websocket
{
    // this is the client implementation used by browsers
    public class WebsocketConnectionWebGl : IConnection
    {
        static int idGenerator = 0;
        static readonly Dictionary<int, WebsocketConnectionWebGl> clients = new Dictionary<int, WebsocketConnectionWebGl>();

        readonly AsyncQueue<byte[]> receivedQueue = new AsyncQueue<byte[]>();

        int nativeRef = 0;
        readonly int id;

        public WebsocketConnectionWebGl()
        {
            id = Interlocked.Increment(ref idGenerator);
        }

        private Uri uri;

        TaskCompletionSource<int> connectCompletionSource;
        public Task ConnectAsync(Uri uri)
        {
            clients[id] = this;
            connectCompletionSource = new TaskCompletionSource<int>();

            this.uri = uri;

            nativeRef = SocketCreate(uri.ToString(), id, OnOpen, OnData, OnClose);
            return connectCompletionSource.Task;
        }

        public void Disconnect()
        {
            SocketClose(nativeRef);
        }

        // send the data or throw exception
        public UniTask SendAsync(ArraySegment<byte> segment)
        {
            SocketSend(nativeRef, segment.Array, segment.Count);
            return UniTask.CompletedTask;
        }

#region Javascript native functions
        [DllImport("__Internal")]
        static extern int SocketCreate(
            string url,
            int id,
            Action<int> onpen,
            Action<int, IntPtr, int> ondata,
            Action<int> onclose);

        [DllImport("__Internal")]
        static extern int SocketState(int socketInstance);

        [DllImport("__Internal")]
        static extern void SocketSend(int socketInstance, byte[] ptr, int length);

        [DllImport("__Internal")]
        static extern void SocketClose(int socketInstance);

#endregion

#region Javascript callbacks

        [MonoPInvokeCallback(typeof(Action))]
        public static void OnOpen(int id)
        {
            clients[id].connectCompletionSource.SetResult(0);
        }

        [MonoPInvokeCallback(typeof(Action))]
        public static void OnClose(int id)
        {
            clients[id].receivedQueue.Enqueue(null);
            clients.Remove(id);
        }

        [MonoPInvokeCallback(typeof(Action))]
        public static void OnData(int id, IntPtr ptr, int length)
        {
            byte[] data = new byte[length];
            Marshal.Copy(ptr, data, 0, length);

            clients[id].receivedQueue.Enqueue(data);
        }

        public async UniTask<bool> ReceiveAsync(MemoryStream buffer)
        {

            byte [] data = await receivedQueue.DequeueAsync();
            buffer.SetLength(0);

            if (data == null)
                return false;

            buffer.Write(data, 0, data.Length);
            Debug.Log("Received data" + BitConverter.ToString(data));

            return true;
        }

        public EndPoint GetEndPointAddress()
        {
            return new DnsEndPoint(uri.Host, uri.Port);
        }
#endregion
    }
}

#endif

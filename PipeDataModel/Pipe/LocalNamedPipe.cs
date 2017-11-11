﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.IO.Pipes;
using PipeDataModel.DataTree;
using System.Runtime.Serialization.Formatters.Binary;
using System.Diagnostics;

namespace PipeDataModel.Pipe
{
    public class LocalNamedPipe : Pipe
    {
        #region-fields
        private string _name;
        private Action _callBack = null;
        private NamedPipeServerStream _pipeServer;
        private NamedPipeClientStream _pipeClient;
        private bool _isActive = false;
        private IAsyncResult _result;
        #endregion

        #region-properties
        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }
        #endregion

        #region-constructors
        public LocalNamedPipe(string name)
        {
            _name = name;
        }
        public LocalNamedPipe(string name, Action callBack) : this(name)
        {
            _callBack = callBack;
        }
        #endregion

        #region-base class implementation
        protected override void PushData(DataNode data)
        {
            if (_isActive)
            {
                DataNode oldData = PullData();
                //if the new data and the old data are same then we don't need to resend it so we bail out.
                if (oldData.Equals(data)) { return; }
            }

            _pipeServer = new NamedPipeServerStream(_name, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            _result = _pipeServer.BeginWaitForConnection(ar => {
                _isActive = false;
                _pipeServer.EndWaitForConnection(ar);
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(_pipeServer, data);
                _pipeServer.WaitForPipeDrain();
                _pipeServer.Flush();
                _pipeServer.Close();
                _pipeServer.Dispose();
                _callBack.Invoke();
            }, _pipeServer);
            _isActive = true;
        }

        protected override DataNode PullData()
        {
            object received = null;
            BinaryFormatter bf = new BinaryFormatter();
            try
            {
                _pipeClient = new NamedPipeClientStream(".", _name, PipeDirection.In, PipeOptions.Asynchronous);
                _pipeClient.Connect(50);
                received = bf.Deserialize(_pipeClient);
                _pipeClient.Close();
                _pipeClient = null;
            }
            catch(TimeoutException e)
            {
                return null;
            }
            return received != null ? (DataNode)received : null;
        }
        #endregion

        #region-methods
        #endregion
    }
}

/*
 * Copyright 2011 Google Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.IO;
using System.Net;
using BitCoinSharp.Common;
using BitCoinSharp.IO;
using Org.BouncyCastle.Math;

namespace BitCoinSharp
{
    /// <summary>
    /// A PeerAddress holds an IP address and port number representing the network location of
    /// a peer in the BitCoin P2P network. It exists primarily for serialization purposes.
    /// </summary>
    [Serializable]
    public class PeerAddress : Message
    {
        internal IPAddress Addr { get; private set; }
        internal int Port { get; private set; }
        private ulong _services;
        private uint _time;

        /// <exception cref="BitCoinSharp.ProtocolException" />
        public PeerAddress(NetworkParameters @params, byte[] payload, int offset, uint protocolVersion)
            : base(@params, payload, offset, protocolVersion)
        {
        }

        public PeerAddress(IPAddress addr, int port, uint protocolVersion)
        {
            Addr = addr;
            Port = port;
            ProtocolVersion = protocolVersion;
        }

        /// <exception cref="System.IO.IOException" />
        public override void BitcoinSerializeToStream(Stream stream)
        {
            if (ProtocolVersion >= 31402)
            {
                var secs = UnixTime.ToUnixTime(DateTime.UtcNow);
                Utils.Uint32ToByteStreamLe((uint) secs, stream);
            }
            Utils.Uint64ToByteStreamLe(_services, stream); // nServices.
            // Java does not provide any utility to map an IPv4 address into IPv6 space, so we have to do it by hand.
            var ipBytes = Addr.GetAddressBytes();
            if (ipBytes.Length == 4)
            {
                var v6Addr = new byte[16];
                Array.Copy(ipBytes, 0, v6Addr, 12, 4);
                v6Addr[10] = 0xFF;
                v6Addr[11] = 0xFF;
                ipBytes = v6Addr;
            }
            stream.Write(ipBytes);
            // And write out the port. Unlike the rest of the protocol, address and port is in big endian byte order.
            stream.Write((byte) (Port >> 8));
            stream.Write((byte) Port);
        }

        /// <exception cref="BitCoinSharp.ProtocolException" />
        protected override void Parse()
        {
            // Format of a serialized address:
            //   uint32 timestamp
            //   uint64 services   (flags determining what the node can do)
            //   16 bytes ip address
            //   2 bytes port num
            if (ProtocolVersion > 31402)
                _time = ReadUint32();
            else
                _time = uint.MaxValue;
            _services = ReadUint64();
            var addrBytes = ReadBytes(16);
            if (new BigInteger(addrBytes, 0, 12).Equals(BigInteger.ValueOf(0xFFFF)))
            {
                var newBytes = new byte[4];
                Array.Copy(addrBytes, 12, newBytes, 0, 4);
                addrBytes = newBytes;
            }
            Addr = new IPAddress(addrBytes);
            Port = (Bytes[Cursor++] << 8) | Bytes[Cursor++];
        }

        public override string ToString()
        {
            return "[" + Addr + "]:" + Port;
        }
    }
}
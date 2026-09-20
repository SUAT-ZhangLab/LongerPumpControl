using System;
using System.IO;
using System.IO.Ports;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace LongerControl {
    public interface IWire : IDisposable { byte[] Exchange(byte[] request, int expectedLength); }
    public sealed class SerialWire : IWire {
        readonly SerialPort port;
        public SerialWire(string name, int baud, Parity parity) {
            port = new SerialPort(name, baud, parity, 8, StopBits.One);
            port.Handshake = Handshake.None; port.DtrEnable = false; port.RtsEnable = false;
            port.ReadTimeout = 80; port.WriteTimeout = 800;
            try { port.Open(); } catch { port.Dispose(); throw; }
        }
        public byte[] Exchange(byte[] request, int expectedLength) {
            Thread.Sleep(5); port.DiscardInBuffer(); port.Write(request, 0, request.Length);
            var data = new List<byte>(); var sw = Stopwatch.StartNew();
            // Read to frame length; never accept a partial frame or a transmit echo as success.
            while (sw.ElapsedMilliseconds < 1000) {
                try { data.Add((byte)port.ReadByte()); } catch (TimeoutException) { continue; }
                int n = data.Count;
                if (n >= 3) {
                    int size = (data[1] & 0x80) != 0 ? 5 : (data[1] == 3 ? 5 + data[2] : expectedLength);
                    if (n >= size) return data.ToArray();
                }
            }
            throw new TimeoutException("泵无完整响应，请检查电源、通信设置及线缆。收到 " + data.Count + " 字节。");
        }
        public void Dispose() { port.Dispose(); }
    }
    public sealed class PumpClient : IDisposable {
        readonly IWire wire; readonly byte address;
        public PumpClient(IWire wire, byte address) {
            if (address < 1 || address > 247) throw new ArgumentOutOfRangeException("address");
            this.wire = wire; this.address = address;
        }
        public static ushort Crc(byte[] bytes, int count) {
            ushort c = 65535;
            for (int i=0; i<count; i++) { c ^= bytes[i]; for (int j=0;j<8;j++) c=(ushort)((c&1)!=0 ? (c>>1)^0xA001 : c>>1); }
            return c;
        }
        public static byte[] Frame(params byte[] body) {
            var result = new byte[body.Length+2]; Array.Copy(body,result,body.Length);
            ushort crc=Crc(body,body.Length); result[body.Length]=(byte)crc; result[body.Length+1]=(byte)(crc>>8); return result;
        }
        byte[] Transaction(byte[] request, int length) {
            var r=wire.Exchange(request,length);
            if(r==null || r.Length<5) throw new IOException("响应帧不完整");
            if(Crc(r,r.Length-2)!=(r[r.Length-2]|r[r.Length-1]<<8)) throw new IOException("响应 CRC 校验失败");
            if(r[0]!=address) throw new IOException("响应设备地址不匹配");
            if(r[1]==(request[1]|0x80) && r.Length==5) throw new IOException("泵拒绝命令，Modbus 异常码 " + r[2]);
            if(r[1]!=request[1] || r.Length!=length) throw new IOException("响应功能码或长度不匹配");
            return r;
        }
        public ushort[] Read(ushort register, ushort count) {
            if(count<1 || count>125) throw new ArgumentOutOfRangeException("count");
            byte[] r=Transaction(Frame(address,3,(byte)(register>>8),(byte)register,(byte)(count>>8),(byte)count),5+2*count);
            if(r[2]!=count*2) throw new IOException("响应字节数不匹配");
            var values=new ushort[count]; for(int i=0;i<count;i++) values[i]=(ushort)(r[3+2*i]<<8|r[4+2*i]); return values;
        }
        // Function 16 has a distinct acknowledgement, so an adapter echo cannot confirm a write.
        public void Write(ushort register, params ushort[] values) {
            if(values.Length<1 || values.Length>123) throw new ArgumentOutOfRangeException("values");
            var body=new List<byte>{address,16,(byte)(register>>8),(byte)register,0,(byte)values.Length,(byte)(2*values.Length)};
            foreach(var v in values) { body.Add((byte)(v>>8)); body.Add((byte)v); }
            var r=Transaction(Frame(body.ToArray()),8);
            if(r[2]!=(byte)(register>>8) || r[3]!=(byte)register || r[4]!=0 || r[5]!=values.Length) throw new IOException("写入确认地址或数量不匹配；请重新读取泵状态");
        }
        public PumpStatus Status() { return new PumpStatus(Read(0x100,17)); }
        public ushort[] Settings() { return Read(0x60,30); }
        public PumpIdentity Identity() { return new PumpIdentity(Read(0x120,28)); }
        public void Command(ushort register, ushort value) {
            Write(register,value);
            if(register!=1&&register!=13)return;
            int expected=register==1?(value==0?3:1):(value==1?2:1);
            var deadline=Stopwatch.StartNew();
            while(deadline.ElapsedMilliseconds<2200) {
                Thread.Sleep(100);var status=Status();
                if(status.State==expected)return;
                if(status.Errors!=0&&expected!=3)throw new IOException("泵报告故障："+status.ErrorText);
            }
            throw new IOException("指令已被接收，但未确认预期状态；请查看泵面板并重新读取状态。");
        }
        public void Apply(SortedDictionary<ushort,ushort[]> changes) {
            if(Status().State!=3) throw new InvalidOperationException("仅在泵处于停止状态时允许修改参数。");
            // Each parameter is written with its unit in one function-16 transaction.
            // There is deliberately no retry or rollback: a lost acknowledgement has uncertain outcome.
            foreach(var item in changes) {
                Write(item.Key,item.Value);
                if(!Read(item.Key,(ushort)item.Value.Length).SequenceEqual(item.Value)) throw new IOException("参数回读与写入不一致：0x"+item.Key.ToString("X4")+"。部分参数可能已生效，请重新读取。");
            }
        }
        public void Dispose() { wire.Dispose(); }
    }
    public static class Units {
        public static double Decode(ushort value, ushort unit, bool flow) {
            int u=unit; int basis=100; double divisor=1;
            if(flow) { if(u>=294&&u<=306) {basis=300;divisor=60;} else if(u>=194&&u<=206) {basis=200;divisor=1.0/60;} else if(u<94||u>106) throw new InvalidDataException("未知流量单位 "+u); }
            else if(u<92||u>106) throw new InvalidDataException("未知液量单位 "+u);
            return value*Math.Pow(10,u-basis)/1000/divisor;
        }
        public static ushort[] Encode(double value, int minUnit, int maxUnit, int basis, int maxValue) {
            if(double.IsNaN(value)||double.IsInfinity(value)||value<=0) throw new ArgumentException("参数必须是大于 0 的有限数字。");
            for(int u=minUnit;u<=maxUnit;u++) {
                double scaled=value/Math.Pow(10,u-basis), rounded=Math.Round(scaled,0,MidpointRounding.AwayFromZero);
                if(rounded>=1&&rounded<=maxValue && Math.Abs(scaled-rounded)<=Math.Max(1e-7,Math.Abs(scaled)*1e-8)) return new ushort[]{(ushort)rounded,(ushort)u};
            }
            throw new ArgumentException("数值精度或范围超出泵的寄存器限制，请减少有效数字。");
        }
        public static string Number(double value) { return value.ToString("0.#########",CultureInfo.InvariantCulture); }
        public static string Flow(ushort v,ushort u) { try{return Number(Decode(v,u,true))+" mL/min";} catch{return v+" / 未知单位 "+u;} }
        public static string Volume(ushort v,ushort u) { try{return Number(Decode(v,u,false))+" mL";} catch{return v+" / 未知单位 "+u;} }
    }
    public sealed class PumpStatus {
        public readonly ushort[] Raw; public int State {get{return Raw[1]&15;}} public int Errors {get{return Raw[0];}}
        public PumpStatus(ushort[] raw) { if(raw.Length!=17)throw new ArgumentException("status");Raw=raw; }
        public string StateText {get{switch(State){case 0:return "空闲（保留）";case 1:return "运行中";case 2:return "已暂停";case 3:return "已停止";case 4:return "分配间隔";case 5:return "循环间隔";default:return "未知状态 "+State;}}}
        public string ErrorText {get{if(Errors==0)return "无故障";var s=new List<string>();if((Errors&1)!=0)s.Add("电机堵转");if((Errors&2)!=0)s.Add("异常信号");if((Errors&4)!=0)s.Add("电压低");if((Errors&~7)!=0)s.Add("未知故障 0x"+Errors.ToString("X4"));return string.Join(" / ",s);}}
        public string Direction {get{return Raw[10]==1?"注射":Raw[10]==2?"抽取":"未知";}}
    }
    public sealed class PumpIdentity {
        public string Model,Serial,Hardware,Firmware;
        public PumpIdentity(ushort[] r) {
            if(r.Length!=28)throw new InvalidDataException("设备信息长度不正确");
            var bytes=new byte[r.Length*2];for(int i=0;i<r.Length;i++){bytes[i*2]=(byte)(r[i]>>8);bytes[i*2+1]=(byte)r[i];}
            Func<int,int,string> field=(start,length)=>Encoding.ASCII.GetString(bytes,start,length).Split('\r','\0')[0].Trim();
            Model=field(0,16);Serial=field(16,24);Hardware=field(40,8);Firmware=field(48,8);
        }
    }
    // Explicitly selected demonstration mode; it never opens a serial port.
    public sealed class DemoWire : IWire {
        readonly ushort[] regs=new ushort[0x200]; DateTime last=DateTime.UtcNow; double volume;
        public DemoWire(int index) {
            string info="LSP12-1B\r".PadRight(16,'\0')+("DEMO000"+(index+1)+"\r").PadRight(24,'\0')+"A-01\r".PadRight(8,'\0')+"1.0.3.0\r";
            for(int i=0;i<28;i++)regs[0x120+i]=(ushort)(info[i*2]<<8|info[i*2+1]);
            regs[0x101]=3;regs[0x103]=regs[0x107]=100;regs[0x105]=regs[0x109]=100;regs[0x10A]=1;
            regs[0x60]=1;regs[0x61]=(ushort)(index==0?5:1);regs[0x62]=103;regs[0x63]=8000;regs[0x64]=98;
            regs[0x68]=1;regs[0x69]=103;regs[0x6A]=regs[0x6C]=100;regs[0x6B]=regs[0x6D]=100;regs[0x6E]=45;
            regs[0x70]=regs[0x72]=1;regs[0x71]=regs[0x73]=100;regs[0x74]=1;
            regs[0x75]=regs[0x77]=10;regs[0x76]=regs[0x78]=102;
        }
        public byte[] Exchange(byte[] q,int expected) {
            int r=q[2]<<8|q[3],n=q[4]<<8|q[5];
            double elapsed=(DateTime.UtcNow-last).TotalMinutes;last=DateTime.UtcNow;
            if(regs[0x101]==1)volume+=elapsed*100;
            regs[0x104]=(ushort)Math.Min(65535,volume);regs[0x102]=(ushort)(regs[0x101]==1?100:0);
            if(q[1]==3){var b=new List<byte>{q[0],3,(byte)(n*2)};for(int i=0;i<n;i++){b.Add((byte)(regs[r+i]>>8));b.Add((byte)regs[r+i]);}return PumpClient.Frame(b.ToArray());}
            for(int i=0;i<n;i++)regs[r+i]=(ushort)(q[7+2*i]<<8|q[8+2*i]);
            if(r==1)regs[0x101]=(ushort)(regs[1]==1?1:3);
            if(r==13)regs[0x101]=(ushort)(regs[13]==1?2:1);
            return PumpClient.Frame(q[0],16,q[2],q[3],q[4],q[5]);
        }
        public void Dispose() {}
    }
}

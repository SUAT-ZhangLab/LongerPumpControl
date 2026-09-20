using System;
using System.Drawing;
using System.Linq;
using LongerControl;
class LayoutPortTests {
    static int count;
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);count++;Console.WriteLine("PASS "+text);}
    static void Ports(string[] available,string[] old,bool[] locked,string[] preferred,string[] expected,string text,int changed=-1){Check(PortSelection.Assign(available,old,locked,preferred,changed).SequenceEqual(expected),text);}
    static int Main(){try{
        var unlocked=new[]{false,false};var none=new string[0];
        Ports(new[]{"COM9","COM8"},new[]{"",""},unlocked,none,new[]{"COM8","COM9"},"first launch assigns distinct ports");
        Ports(new[]{"COM3","COM12","COM7"},new[]{"",""},unlocked,new[]{"COM12","COM7"},new[]{"COM7","COM12"},"prefer FTDI adapters over other COM ports, numeric order");
        Ports(new[]{"COM7","COM12"},new[]{"COM7","COM7"},unlocked,none,new[]{"COM7","COM12"},"repair duplicate saved settings");
        Ports(new[]{"COM12","COM7"},new[]{"COM12","COM7"},unlocked,none,new[]{"COM12","COM7"},"preserve valid manual choices");
        Ports(new[]{"COM4","COM5"},new[]{"COM8","COM9"},unlocked,none,new[]{"COM4","COM5"},"replace stale ports after computer migration");
        Ports(new[]{"COM7"},new[]{"",""},unlocked,none,new[]{"COM7",""},"one port leaves second selector empty");
        Ports(none,new[]{"COM7","COM8"},unlocked,none,new[]{"",""},"no ports leaves both empty");
        Ports(new[]{"COM8","COM9"},new[]{"COM7","COM7"},new[]{true,false},none,new[]{"COM7","COM8"},"disconnected cable does not reassign an open session");
        Ports(new[]{"COM7","COM8"},new[]{"COM7","COM7"},unlocked,none,new[]{"COM8","COM7"},"manual choice moves the other unconnected selection",1);
        Ports(new[]{"COM7","COM8"},new[]{"com7","COM7"},unlocked,none,new[]{"COM7","COM8"},"case insensitive uniqueness");
        var minimum=new Size(1124,812);foreach(var item in new[]{Tuple.Create(new Size(1280,680),1f),Tuple.Create(new Size(1000,700),1f),Tuple.Create(new Size(1920,1000),1.5f),Tuple.Create(new Size(3000,1800),2f)}){float s=AdaptiveForm.FitScale(item.Item1,minimum,item.Item2,0);Check(s<=item.Item2&&minimum.Width*s<=item.Item1.Width+1&&minimum.Height*s<=item.Item1.Height+1,"auto layout fits "+item.Item1+" at DPI factor "+item.Item2);}
        Check(AdaptiveForm.FitScale(new Size(1280,680),minimum,1.5f,1)==1.5f,"manual zoom preserves requested DPI scale with scrolling");
        Console.WriteLine(count+" layout/port checks passed");return 0;
    }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}

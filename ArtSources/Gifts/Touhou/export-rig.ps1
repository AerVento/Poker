# Run with Windows PowerShell 5.1. Technical atlas extraction only; artwork and transparent alpha come from imagegen.
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing,System -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Generic;
public static class RigAtlasExport
{
    public static string Extract(string source, string destination, Rectangle area)
    {
        using(var input = new Bitmap(source))
        using(var crop = input.Clone(area, PixelFormat.Format32bppArgb))
        {
            int w=crop.Width,h=crop.Height;
            var data=crop.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            var pixels=new byte[data.Stride*h];Marshal.Copy(data.Scan0,pixels,0,pixels.Length);int stride=data.Stride;crop.UnlockBits(data);
            var seen=new bool[w*h];var queue=new Queue<int>();var largest=new List<int>();
            for(int i=0;i<w*h;i++)
            {
                if(seen[i] || pixels[(i/w)*stride+(i%w)*4+3]<8) continue;
                var component=new List<int>();queue.Enqueue(i);seen[i]=true;
                while(queue.Count>0)
                {
                    int p=queue.Dequeue();component.Add(p);int x=p%w,y=p/w;
                    foreach(int n in new[]{x>0?p-1:-1,x<w-1?p+1:-1,y>0?p-w:-1,y<h-1?p+w:-1})
                        if(n>=0&&!seen[n]&&pixels[(n/w)*stride+(n%w)*4+3]>=8){seen[n]=true;queue.Enqueue(n);}
                }
                if(component.Count>largest.Count)largest=component;
            }
            if(largest.Count<1000)throw new Exception("Missing atlas part: "+destination);
            int left=w,right=0,top=h,bottom=0;
            var selected=new bool[w*h];
            foreach(int p in largest){selected[p]=true;left=Math.Min(left,p%w);right=Math.Max(right,p%w);top=Math.Min(top,p/w);bottom=Math.Max(bottom,p/w);}
            // Clear only other disconnected atlas pieces, retaining the source alpha on this piece.
            for(int i=0;i<w*h;i++) if(!selected[i])pixels[(i/w)*stride+(i%w)*4+3]=0;
            data=crop.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);Marshal.Copy(pixels,0,data.Scan0,pixels.Length);crop.UnlockBits(data);
            using(var output=crop.Clone(new Rectangle(left,top,right-left+1,bottom-top+1),PixelFormat.Format32bppArgb))output.Save(destination,ImageFormat.Png);
            return destination+": "+(right-left+1)+"x"+(bottom-top+1);
        }
    }
}
'@
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
$names=@{Sakuya='TheWorld';Kaguya='HourOfDaybreak';Reimu='FantasySeal';Marisa='MasterSpark';Yuyuko='ResurrectionButterfly'}
foreach($actor in @('Sakuya','Kaguya','Reimu','Marisa','Yuyuko')) {
    $regions=@{
        head=@(0,0,600,540);body=@(600,0,400,530);lower=@(980,0,556,585)
        hair=@(0,540,580,484);left=@(530,550,470,474);right=@(1000,550,536,474)
    }
    if($actor -eq 'Marisa'){$regions.head=@(0,0,650,540)}
    # Remove the torso's overlapping apron/kimono hem; sleeves cover the shoulder joins.
    if($actor -eq 'Sakuya'){$regions.body=@(610,125,275,250)}
    if($actor -eq 'Kaguya'){$regions.body=@(682,88,200,294)}
    if($actor -eq 'Yuyuko'){$regions.body=@(620,120,330,290)}
    foreach($part in @('head','body','lower','hair','left','right')) {
        $r=$regions[$part]
        [RigAtlasExport]::Extract("$PSScriptRoot/$actor/rig-atlas.png","$root/Assets/Art/Gifts/$($names[$actor])/rig-$part.png",[Drawing.Rectangle]::new($r[0],$r[1],$r[2],$r[3]))
    }
}


# Preserve the approved open-eye edit after extracting the original atlas.
Copy-Item "$PSScriptRoot/Yuyuko/rig-head-open.png" "$root/Assets/Art/Gifts/ResurrectionButterfly/rig-head.png" -Force

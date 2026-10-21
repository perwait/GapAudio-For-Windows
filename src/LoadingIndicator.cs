using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace GapGone {
    public sealed class LoadingIndicator : FlowLayoutPanel {
        readonly StyledLabel label = new StyledLabel {AutoSize=false,Width=126,Height=34,TextAlign=System.Drawing.ContentAlignment.MiddleLeft};
        readonly Button cancel = MainForm.Button("取消本次加载");
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer {Interval=200};
        readonly Stopwatch elapsed = new Stopwatch();
        CancellationTokenSource source; int percent;
        public LoadingIndicator() {
            Dock=DockStyle.Fill;WrapContents=false;Margin=Padding.Empty;AutoSize=true;Controls.Add(label);Controls.Add(cancel);label.Visible=cancel.Visible=false;
            cancel.Click+=(s,e)=>{if(source!=null){source.Cancel();cancel.Enabled=false;label.Text="正在取消加载…";}};
            timer.Tick+=(s,e)=>{if(source!=null && elapsed.Elapsed.TotalSeconds>=5){label.Visible=cancel.Visible=true;if(!source.IsCancellationRequested)label.Text="加载进度："+Math.Min(99,percent)+"%";}};
        }
        public void Begin(CancellationTokenSource token) {source=token;percent=0;elapsed.Restart();cancel.Enabled=true;label.Visible=cancel.Visible=false;timer.Start();}
        public Action<int> Progress {get {return p=>Interlocked.Exchange(ref percent,Math.Max(0,Math.Min(100,p)));}}
        public void End() {timer.Stop();elapsed.Stop();source=null;label.Visible=cancel.Visible=false;}
        protected override void Dispose(bool disposing) {if(disposing)timer.Dispose();base.Dispose(disposing);}
    }
}

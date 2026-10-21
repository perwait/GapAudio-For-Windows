using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GapGone {
    public sealed class TrackDialog : Form {
        readonly ListBox list = new ListBox { Dock=DockStyle.Fill, DrawMode=DrawMode.OwnerDrawFixed, ItemHeight=64, IntegralHeight=false, BorderStyle=BorderStyle.None };
        public int SelectedTrack { get { return list.SelectedIndex; } }
        public TrackDialog(List<AudioInfo> tracks, int current) {
            Icon=AppAssets.Icon(); Text="选择要编辑的音轨"; Size=new Size(700,360); MinimumSize=new Size(560,280); StartPosition=FormStartPosition.CenterParent; Font=new Font("Microsoft YaHei UI",9); ShowInTaskbar=false;
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Padding=new Padding(12)};
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,36));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            layout.Controls.Add(new StyledLabel {Dock=DockStyle.Fill,Text="检测到 "+tracks.Count+" 条音轨，请选择要编辑的一条。"},0,0);layout.Controls.Add(list,0,1);
            var actions=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};
            var cancel=MainForm.Button("取消");cancel.DialogResult=DialogResult.Cancel;var ok=MainForm.Button("加载所选音轨");ok.DialogResult=DialogResult.OK;
            actions.Controls.Add(cancel);actions.Controls.Add(ok);layout.Controls.Add(actions,0,2);Controls.Add(layout);AcceptButton=ok;CancelButton=cancel;
            foreach(var track in tracks)list.Items.Add(track.TrackDescription);
            list.SelectedIndex=current>=0 && current<tracks.Count?current:Math.Max(0,tracks.FindIndex(x=>x.DefaultTrack));
            list.DrawItem+=(s,e)=>{if(e.Index<0)return;bool selected=(e.State&DrawItemState.Selected)!=0;using(var brush=new SolidBrush(selected?Palette.Selection:Palette.Surface))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,(string)list.Items[e.Index],Font,new Rectangle(e.Bounds.X+10,e.Bounds.Y+8,e.Bounds.Width-20,e.Bounds.Height-10),Palette.Text,TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);e.DrawFocusRectangle();};
            list.DoubleClick+=(s,e)=>{if(list.SelectedIndex>=0){DialogResult=DialogResult.OK;Close();}};Palette.Apply(this);
        }
        public static int Choose(IWin32Window owner,List<AudioInfo> tracks,int current=-1) {if(tracks.Count==1)return 0;using(var dialog=new TrackDialog(tracks,current))return dialog.ShowDialog(owner)==DialogResult.OK?dialog.SelectedTrack:-1;}
        public static DialogResult ConfirmMp3(IWin32Window owner,AudioInfo info) {
            using(var dialog=CreateMp3Dialog(info))return dialog.ShowDialog(owner);
        }
        public static ChoiceDialog CreateMp3Dialog(AudioInfo info) { return new ChoiceDialog("多声道输出","当前音轨为 "+info.ChannelDescription+"，MP3 最多支持 2 条声道。\n\n可转换为 FLAC 保留全部声道，或混音为双声道 MP3。\n取消将保持当前选择。","改用 FLAC","双声道 MP3"); }
    }
}

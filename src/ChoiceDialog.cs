using System;
using System.Drawing;
using System.Windows.Forms;

namespace GapGone {
    public sealed class ChoiceDialog : Form {
        public ChoiceDialog(string title, string message, string confirm, string secondary = null, string cancel = "取消") {
            Text=title; Icon=AppAssets.Icon(); Font=new Font("Microsoft YaHei UI",10);
            StartPosition=FormStartPosition.CenterParent; ShowInTaskbar=false; MinimizeBox=MaximizeBox=false;
            FormBorderStyle=FormBorderStyle.FixedDialog; AutoScaleMode=AutoScaleMode.Font;
            int bodyHeight=TextRenderer.MeasureText(message,Font,new Size(576,1000),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+24;
            ClientSize=new Size(620,Math.Max(240,bodyHeight+114));
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(22,18,22,14)};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,34));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,46));
            layout.Controls.Add(new StyledLabel {Text=title,Dock=DockStyle.Fill,Font=new Font(Font,FontStyle.Bold),Margin=Padding.Empty},0,0);
            layout.Controls.Add(new StyledLabel {Text=message,Dock=DockStyle.Fill,Margin=new Padding(0,6,0,12)},0,1);
            var actions=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Margin=Padding.Empty};
            var cancelButton=MainForm.Button(cancel);cancelButton.DialogResult=DialogResult.Cancel;actions.Controls.Add(cancelButton);CancelButton=cancelButton;AcceptButton=cancelButton;
            if(secondary!=null){var second=MainForm.Button(secondary);second.DialogResult=DialogResult.No;actions.Controls.Add(second);}
            var accept=MainForm.Button(confirm);accept.DialogResult=DialogResult.Yes;actions.Controls.Add(accept);
            layout.Controls.Add(actions,0,2);Controls.Add(layout);Palette.Apply(this);
        }
        public static DialogResult Ask(IWin32Window owner,string title,string message,string confirm,string secondary=null,string cancel="取消") {
            using(var dialog=new ChoiceDialog(title,message,confirm,secondary,cancel))return dialog.ShowDialog(owner);
        }
    }
}

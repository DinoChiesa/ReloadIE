using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;


namespace ReloadIt
{
    class StatusMessage
    {
        public String Message { get; set; }
        public DateTime Time { get; set; }
        public Int32 Flavor { get; set; }
    }

    class MessageList : ObservableCollection<StatusMessage>
    {
        public void Add(String msg)
        {
            this.AddInfo(msg);
        }

        public void AddInfo(String msg)
        {
            base.Add(new StatusMessage { Message = msg, Time = DateTime.Now, Flavor = 0 });
        }

        public void AddAlert(String msg)
        {
            base.Add(new StatusMessage { Message = msg, Time = DateTime.Now, Flavor = 1 });
        }
    }



    class AppState : System.ComponentModel.INotifyPropertyChanged
    {
        private MessageList messages;
        private bool showInfos, showAlerts;

        public AppState()
        {
            showInfos = showAlerts = true;
            messages = new MessageList();
            messages.CollectionChanged +=
                new NotifyCollectionChangedEventHandler(messages_CollectionChanged);
        }

        private void messages_CollectionChanged(object sender,
                                              NotifyCollectionChangedEventArgs e)
        {
            NotifyPropertyChanged("UnacknowledgedAlertCount");
            NotifyPropertyChanged("MessageButtonContent");
            NotifyPropertyChanged("MessageButtonForeColor");
            NotifyPropertyChanged("MessageButtonBackColor");

        }


        public int AckAlerts()
        {
            AlertAckCount = this.AlertsOnly.Count();
            return AlertAckCount;
        }


        private void SetMessagesFilter()
        {
            if (showInfos && showAlerts)
                messagesCv.Filter = null;
            else if (showInfos)
                messagesCv.Filter = item => ((StatusMessage)item).Flavor==0;
            else if (showAlerts)
                messagesCv.Filter = item => ((StatusMessage)item).Flavor==1;
            else
                messagesCv.Filter = elt => false;
        }


        public MessageList Messages
        {
            get { return messages; }
        }


        private IEnumerable<StatusMessage> AlertsOnly
        {
            get
            {
                var selection = from a in messages where a.Flavor == 1 select a;
                return selection;
            }
        }

        private int alertAckCount;
        public int AlertAckCount
        {
            get { return alertAckCount; }
            set
            {
                alertAckCount = value;
                NotifyPropertyChanged("AlertAckCount");
                NotifyPropertyChanged("MessageButtonContent");
                NotifyPropertyChanged("UnacknowledgedAlertCount");
                NotifyPropertyChanged("MessageButtonForeColor");
                NotifyPropertyChanged("MessageButtonBackColor");
            }
        }

        public int UnacknowledgedAlertCount
        {
            get
            {
                return this.AlertsOnly.Count() - alertAckCount;
            }
        }

        private int numReloads;
        public int NumReloads
        {
            get { return numReloads; }
            set
            {
                numReloads = value;
                NotifyPropertyChanged("NumReloads");
                NotifyPropertyChanged("ReloadCountText");
            }
        }

        public String ReloadCountText
        {
            get
            {
                if (numReloads == 0) return "-";
                return "# " + numReloads;
            }
        }

        public object MessageButtonContent
        {
            get
            {
                int n = UnacknowledgedAlertCount;
                if (n == 0) return "no new alerts";
                if (n == 1) return "1 new alert";
                return n + " new alerts";
            }
        }

        public String MessageButtonBackColor
        {
            get
            {
                int n = UnacknowledgedAlertCount;
                if (n == 0) return "#F2FFF6"; // off white
                return "#FF573D";             // reddish
            }
        }

        public String MessageButtonForeColor
        {
            get
            {
                int n = UnacknowledgedAlertCount;
                if (n == 0) return "#A3A3A3"; // light grey
                return "#000000";             // black
            }
        }


        public bool ShowInfos
        {
            get { return showInfos; }
            set
            {
                showInfos = value;
                SetMessagesFilter();
                NotifyPropertyChanged("ShowInfos");
            }
        }

        public bool ShowAlerts
        {
            get { return showAlerts; }
            set
            {
                showAlerts = value;
                SetMessagesFilter();
                NotifyPropertyChanged("ShowAlerts");
            }
        }


        private System.ComponentModel.ICollectionView messagesCv;
        public System.ComponentModel.ICollectionView MessagesCv
        {
            get
            {
                if (messagesCv == null)
                    messagesCv = System.Windows.Data.CollectionViewSource.GetDefaultView(Messages);
                return messagesCv;
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(info));
            }
        }
    }
}

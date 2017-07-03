using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Resources.Media;
using Sitecore.Shell.Applications.Dialogs;
using Sitecore.Shell.Framework;
using Sitecore.Utils;
using Sitecore.Web;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;
using Sitecore.Web.UI.WebControls;
using Sitecore.Xml;

namespace Sitecore.Support.Shell.Applications.Dialogs.GeneralLink
{
    public class GeneralLinkForm : LinkForm
    {
        protected Edit Class;

        protected Literal Custom;

        protected Edit CustomTarget;

        protected DataContext InternalLinkDataContext;

        protected TreeviewEx InternalLinkTreeview;

        protected Border InternalLinkTreeviewContainer;

        protected Memo JavascriptCode;

        protected Edit LinkAnchor;

        protected Border MailToContainer;

        protected Edit MailToLink;

        protected DataContext MediaLinkDataContext;

        protected TreeviewEx MediaLinkTreeview;

        protected Border MediaLinkTreeviewContainer;

        protected Border MediaPreview;

        protected Border Modes;

        protected Edit Querystring;

        protected Literal SectionHeader;

        protected Combobox Target;

        protected Edit Text;

        protected Edit Title;

        protected Scrollbox TreeviewContainer;

        protected Button UploadMedia;

        protected Edit Url;

        protected Border UrlContainer;

        private string CurrentMode
        {
            get
            {
                string text = base.ServerProperties["current_mode"] as string;
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
                return "internal";
            }
            set
            {
                Assert.ArgumentNotNull(value, "value");
                base.ServerProperties["current_mode"] = value;
            }
        }

        public override void HandleMessage(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            if (this.CurrentMode != "media")
            {
                base.HandleMessage(message);
                return;
            }
            Item item = null;
            if (message.Arguments.Count > 0 && ID.IsID(message.Arguments["id"]))
            {
                IDataView dataView = this.MediaLinkTreeview.GetDataView();
                if (dataView != null)
                {
                    item = dataView.GetItem(message.Arguments["id"]);
                }
            }
            if (item == null)
            {
                item = this.MediaLinkTreeview.GetSelectionItem();
            }
            Dispatcher.Dispatch(message, item);
        }

        protected void OnListboxChanged()
        {
            if (this.Target.Value == "Custom")
            {
                this.CustomTarget.Disabled=false;
                this.Custom.Class=string.Empty;
                return;
            }
            this.CustomTarget.Value=string.Empty;
            this.CustomTarget.Disabled=true;
            this.Custom.Class="disabled";
        }

        protected override void OnLoad(EventArgs e)
        {
            Assert.ArgumentNotNull(e, "e");
            base.OnLoad(e);
            if (Context.ClientPage.IsEvent)
            {
                return;
            }
            this.CurrentMode = (base.LinkType ?? string.Empty);
            this.InitControls();
            this.SetModeSpecificControls();
            GeneralLinkForm.RegisterScripts();
        }

        protected void OnMediaOpen()
        {
            Item selectionItem = this.MediaLinkTreeview.GetSelectionItem();
            if (selectionItem != null && selectionItem.HasChildren)
            {
                this.MediaLinkDataContext.SetFolder(selectionItem.Uri);
            }
        }

        protected void OnModeChange(string mode)
        {
            Assert.ArgumentNotNull(mode, "mode");
            this.CurrentMode = mode;
            this.SetModeSpecificControls();
            if (!UIUtil.IsIE())
            {
                SheerResponse.Eval("scForm.browser.initializeFixsizeElements();");
            }
        }

        protected override void OnOK(object sender, EventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            Packet packet = new Packet("link", new string[0]);
            this.SetCommonAttributes(packet);
            string currentMode = this.CurrentMode;
            bool flag;
            if (!(currentMode == "internal"))
            {
                if (!(currentMode == "media"))
                {
                    if (!(currentMode == "external"))
                    {
                        if (!(currentMode == "mailto"))
                        {
                            if (!(currentMode == "anchor"))
                            {
                                if (!(currentMode == "javascript"))
                                {
                                    throw new ArgumentException("Unsupported mode: " + this.CurrentMode);
                                }
                                flag = this.SetJavascriptLinkAttributes(packet);
                            }
                            else
                            {
                                flag = this.SetAnchorLinkAttributes(packet);
                            }
                        }
                        else
                        {
                            flag = this.SetMailToLinkAttributes(packet);
                        }
                    }
                    else
                    {
                        flag = this.SetExternalLinkAttributes(packet);
                    }
                }
                else
                {
                    flag = this.SetMediaLinkAttributes(packet);
                }
            }
            else
            {
                flag = this.SetInternalLinkAttributes(packet);
            }
            if (flag)
            {
                SheerResponse.SetDialogValue(packet.OuterXml);
                base.OnOK(sender, args);
            }
        }

        protected void SelectMediaTreeNode()
        {
            Item selectionItem = this.MediaLinkTreeview.GetSelectionItem();
            if (selectionItem == null)
            {
                return;
            }
            this.UpdateMediaPreview(selectionItem);
        }

        protected void UploadImage()
        {
            Item selectionItem = this.MediaLinkTreeview.GetSelectionItem();
            if (selectionItem != null)
            {
                if (!selectionItem.Access.CanCreate())
                {
                    SheerResponse.Alert("You do not have permission to create a new item here.", new string[0]);
                    return;
                }
                Context.ClientPage.SendMessage(this, "media:upload(edit=1,load=1)");
            }
        }

        private static void HideContainingRow(Control control)
        {
            Assert.ArgumentNotNull(control, "control");
            if (Context.ClientPage.IsEvent)
            {
                SheerResponse.SetStyle(control.ID + "Row", "display", "none");
                return;
            }
            GridPanel gridPanel = control.Parent as GridPanel;
            if (gridPanel == null)
            {
                return;
            }
            gridPanel.SetExtensibleProperty(control, "row.style", "display:none");
        }

        private static void ShowContainingRow(Control control)
        {
            Assert.ArgumentNotNull(control, "control");
            if (Context.ClientPage.IsEvent)
            {
                SheerResponse.SetStyle(control.ID + "Row", "display", string.Empty);
                return;
            }
            GridPanel gridPanel = control.Parent as GridPanel;
            if (gridPanel == null)
            {
                return;
            }
            gridPanel.SetExtensibleProperty(control, "row.style", string.Empty);
        }

        private void InitControls()
        {
            string value = string.Empty;
            string text = base.LinkAttributes["target"];
            string linkTargetValue = LinkForm.GetLinkTargetValue(text);
            if (linkTargetValue == "Custom")
            {
                value = text;
                this.CustomTarget.Disabled=false;
                this.Custom.Class=string.Empty;
            }
            else
            {
                this.CustomTarget.Disabled=true;
                this.Custom.Class="disabled";
            }
            this.Text.Value=base.LinkAttributes["text"];
            this.Target.Value=linkTargetValue;
            this.CustomTarget.Value=value;
            this.Class.Value=base.LinkAttributes["class"];
            this.Querystring.Value=base.LinkAttributes["querystring"];
            this.Title.Value=base.LinkAttributes["title"];
            this.InitMediaLinkDataContext();
            this.InitInternalLinkDataContext();
        }

        private void InitInternalLinkDataContext()
        {
            this.InternalLinkDataContext.GetFromQueryString();
            string queryString = WebUtil.GetQueryString("ro");
            string text = base.LinkAttributes["id"];
            if (!string.IsNullOrEmpty(text) && ID.IsID(text))
            {
                ItemUri folder = new ItemUri(new ID(text), Client.ContentDatabase);
                this.InternalLinkDataContext.SetFolder(folder);
            }
            if (queryString.Length > 0)
            {
                this.InternalLinkDataContext.Root=queryString;
            }
        }

        private void InitMediaLinkDataContext()
        {
            this.MediaLinkDataContext.GetFromQueryString();
            string text = base.LinkAttributes["id"];
            if (this.CurrentMode != "media")
            {
                text = string.Empty;
            }
            if (text.Length == 0)
            {
                text = "/sitecore/media library";
            }
            else
            {
                IDataView dataView = this.MediaLinkTreeview.GetDataView();
                if (dataView == null)
                {
                    return;
                }
                Item item = dataView.GetItem(text);
                if (item != null && item.Parent != null)
                {
                    this.MediaLinkDataContext.SetFolder(item.Uri);
                }
            }
            this.MediaLinkDataContext.AddSelected(new DataUri(text));
            this.MediaLinkDataContext.Root="/sitecore/media library";
        }

        private static void RegisterScripts()
        {
            string script =string.Format("window.Texts = {{ ErrorOcurred: \"{0}\"}};", new object[]
            {
                Translate.Text("An error occured:")
            });
            Context.ClientPage.ClientScript.RegisterClientScriptBlock(Context.ClientPage.GetType(), "translationsScript", script, true);
        }

        private bool SetAnchorLinkAttributes(Packet packet)
        {
            Assert.ArgumentNotNull(packet, "packet");
            string text = this.LinkAnchor.Value;
            if (text.Length > 0 && text.StartsWith("#", StringComparison.InvariantCulture))
            {
                text = text.Substring(1);
            }
            LinkForm.SetAttribute(packet, "url", text);
            LinkForm.SetAttribute(packet, "anchor", text);
            return true;
        }

        private void SetAnchorLinkControls()
        {
            GeneralLinkForm.ShowContainingRow(this.LinkAnchor);
            string text = base.LinkAttributes["anchor"];
            if (base.LinkType != "anchor" && string.IsNullOrEmpty(this.LinkAnchor.Value))
            {
                text = string.Empty;
            }
            if (!string.IsNullOrEmpty(text) && !text.StartsWith("#", StringComparison.InvariantCulture))
            {
                text = "#" + text;
            }
            this.LinkAnchor.Value=text ?? string.Empty;
            this.SectionHeader.Text=Translate.Text("Specify the name of the anchor, e.g. #header1, and any additional properties");
        }

        private void SetCommonAttributes(Packet packet)
        {
            Assert.ArgumentNotNull(packet, "packet");
            LinkForm.SetAttribute(packet, "linktype", this.CurrentMode);
            LinkForm.SetAttribute(packet, "text", this.Text);
            LinkForm.SetAttribute(packet, "title", this.Title);
            LinkForm.SetAttribute(packet, "class", this.Class);
        }

        private bool SetExternalLinkAttributes(Packet packet)
        {
            Assert.ArgumentNotNull(packet, "packet");
            string text = this.Url.Value;
            if (text.Length > 0 && text.IndexOf("://", StringComparison.InvariantCulture) < 0 && !text.StartsWith("/", StringComparison.InvariantCulture))
            {
                text = "http://" + text;
            }
            string linkTargetAttributeFromValue = LinkForm.GetLinkTargetAttributeFromValue(this.Target.Value, this.CustomTarget.Value);
            LinkForm.SetAttribute(packet, "url", text);
            LinkForm.SetAttribute(packet, "anchor", string.Empty);
            LinkForm.SetAttribute(packet, "target", linkTargetAttributeFromValue);
            return true;
        }

        private void SetExternalLinkControls()
        {
            if (base.LinkType == "external" && string.IsNullOrEmpty(this.Url.Value))
            {
                string value = base.LinkAttributes["url"];
                this.Url.Value=value;
            }
            GeneralLinkForm.ShowContainingRow(this.UrlContainer);
            GeneralLinkForm.ShowContainingRow(this.Target);
            GeneralLinkForm.ShowContainingRow(this.CustomTarget);
            this.SectionHeader.Text=Translate.Text("Specify the URL, e.g. http://www.sitecore.net and any additional properties.");
        }

        private bool SetInternalLinkAttributes(Packet packet)
        {
            Assert.ArgumentNotNull(packet, "packet");
            Item selectionItem = this.InternalLinkTreeview.GetSelectionItem();
            if (selectionItem == null)
            {
                Context.ClientPage.ClientResponse.Alert("Select an item.");
                return false;
            }
            string linkTargetAttributeFromValue = LinkForm.GetLinkTargetAttributeFromValue(this.Target.Value, this.CustomTarget.Value);
            string text = this.Querystring.Value;
            if (text.StartsWith("?", StringComparison.InvariantCulture))
            {
                text = text.Substring(1);
            }
            LinkForm.SetAttribute(packet, "anchor", this.LinkAnchor);
            LinkForm.SetAttribute(packet, "querystring", text);
            LinkForm.SetAttribute(packet, "target", linkTargetAttributeFromValue);
            LinkForm.SetAttribute(packet, "id", selectionItem.ID.ToString());
            return true;
        }

        private void SetInternalLinkContols()
        {
            this.LinkAnchor.Value=base.LinkAttributes["anchor"];
            this.InternalLinkTreeviewContainer.Visible=true;
            this.MediaLinkTreeviewContainer.Visible=false;
            GeneralLinkForm.ShowContainingRow(this.TreeviewContainer);
            GeneralLinkForm.ShowContainingRow(this.Querystring);
            GeneralLinkForm.ShowContainingRow(this.LinkAnchor);
            GeneralLinkForm.ShowContainingRow(this.Target);
            GeneralLinkForm.ShowContainingRow(this.CustomTarget);
            this.SectionHeader.Text=Translate.Text("Select the item that you want to create a link to and specify the appropriate properties.");
        }

        private void SetJavaScriptLinkControls()
        {
            GeneralLinkForm.ShowContainingRow(this.JavascriptCode);
            string value = base.LinkAttributes["url"];
            if (base.LinkType!= "javascript" && string.IsNullOrEmpty(this.JavascriptCode.Value))
            {
                value = string.Empty;
            }
            this.JavascriptCode.Value=value;
            this.SectionHeader.Text=Translate.Text("Specify the JavaScript and any additional properties.");
        }

        private bool SetJavascriptLinkAttributes(Packet packet)
        {
            Assert.ArgumentNotNull(packet, "packet");
            string text = this.JavascriptCode.Value;
            if (text.Length > 0 && text.IndexOf("javascript:", StringComparison.InvariantCulture) < 0)
            {
                text = "javascript:" + text;
            }
            LinkForm.SetAttribute(packet, "url", text);
            LinkForm.SetAttribute(packet, "anchor", string.Empty);
            return true;
        }

        private void SetMailLinkControls()
        {
            if (base.LinkType == "mailto" && string.IsNullOrEmpty(this.Url.Value))
            {
                string value = base.LinkAttributes["url"];
                this.MailToLink.Value=value;
            }
            GeneralLinkForm.ShowContainingRow(this.MailToContainer);
            this.SectionHeader.Text=Translate.Text("Specify the email address and any additional properties. To send a test mail use the 'Send a test mail' button.");
        }

        private bool SetMailToLinkAttributes(Packet packet)
        {
            Assert.ArgumentNotNull(packet, "packet");
            string text = this.MailToLink.Value;
            text = StringUtil.GetLastPart(text, ':', text);
            if (!EmailUtility.IsValidEmailAddress(text))
            {
                SheerResponse.Alert("The e-mail address is invalid.", new string[0]);
                return false;
            }
            if (!string.IsNullOrEmpty(text))
            {
                text = "mailto:" + text;
            }
            LinkForm.SetAttribute(packet, "url", text ?? string.Empty);
            LinkForm.SetAttribute(packet, "anchor", string.Empty);
            return true;
        }

        private bool SetMediaLinkAttributes(Packet packet)
        {
            Assert.ArgumentNotNull(packet, "packet");
            Item selectionItem = this.MediaLinkTreeview.GetSelectionItem();
            if (selectionItem == null)
            {
                Context.ClientPage.ClientResponse.Alert("Select a media item.");
                return false;
            }
            string linkTargetAttributeFromValue = LinkForm.GetLinkTargetAttributeFromValue(this.Target.Value, this.CustomTarget.Value);
            LinkForm.SetAttribute(packet, "target", linkTargetAttributeFromValue);
            LinkForm.SetAttribute(packet, "id", selectionItem.ID.ToString());
            return true;
        }

        private void SetMediaLinkControls()
        {
            this.InternalLinkTreeviewContainer.Visible=false;
            this.MediaLinkTreeviewContainer.Visible=true;
            this.MediaPreview.Visible=true;
            this.UploadMedia.Visible=true;
            Item folder = this.MediaLinkDataContext.GetFolder();
            if (folder != null)
            {
                this.UpdateMediaPreview(folder);
            }
            GeneralLinkForm.ShowContainingRow(this.TreeviewContainer);
            GeneralLinkForm.ShowContainingRow(this.Target);
            GeneralLinkForm.ShowContainingRow(this.CustomTarget);
            this.SectionHeader.Text=Translate.Text("Select an item from the media library and specify any additional properties.");
        }

        private void SetModeSpecificControls()
        {
            GeneralLinkForm.HideContainingRow(this.TreeviewContainer);
            this.MediaPreview.Visible=false;
            this.UploadMedia.Visible=false;
            GeneralLinkForm.HideContainingRow(this.UrlContainer);
            GeneralLinkForm.HideContainingRow(this.Querystring);
            GeneralLinkForm.HideContainingRow(this.MailToContainer);
            GeneralLinkForm.HideContainingRow(this.LinkAnchor);
            GeneralLinkForm.HideContainingRow(this.JavascriptCode);
            GeneralLinkForm.HideContainingRow(this.Target);
            GeneralLinkForm.HideContainingRow(this.CustomTarget);
            string currentMode = this.CurrentMode;
            if (!(currentMode == "internal"))
            {
                if (!(currentMode == "media"))
                {
                    if (!(currentMode == "external"))
                    {
                        if (!(currentMode == "mailto"))
                        {
                            if (!(currentMode == "anchor"))
                            {
                                if (!(currentMode == "javascript"))
                                {
                                    throw new ArgumentException("Unsupported mode: " + this.CurrentMode);
                                }
                                this.SetJavaScriptLinkControls();
                            }
                            else
                            {
                                this.SetAnchorLinkControls();
                            }
                        }
                        else
                        {
                            this.SetMailLinkControls();
                        }
                    }
                    else
                    {
                        this.SetExternalLinkControls();
                    }
                }
                else
                {
                    this.SetMediaLinkControls();
                }
            }
            else
            {
                this.SetInternalLinkContols();
            }
            foreach (Border border in this.Modes.Controls)
            {
                if (border != null)
                {
                    border.Class=(border.ID.ToLowerInvariant() == this.CurrentMode) ? "selected" : string.Empty;
                }
            }
        }

        private void UpdateMediaPreview(Item item)
        {
            Assert.ArgumentNotNull(item, "item");
            MediaUrlOptions thumbnailOptions = MediaUrlOptions.GetThumbnailOptions(item);
            thumbnailOptions.UseDefaultIcon=true;
            thumbnailOptions.Width=96;
            thumbnailOptions.Height=96;
            thumbnailOptions.Language=item.Language;
            thumbnailOptions.AllowStretch=false;
            string mediaUrl = MediaManager.GetMediaUrl(item, thumbnailOptions);
            this.MediaPreview.InnerHtml="<img src=\"" + mediaUrl + "\" width=\"96px\" height=\"96px\" border=\"0\" alt=\"\" />";
        }
    }
}
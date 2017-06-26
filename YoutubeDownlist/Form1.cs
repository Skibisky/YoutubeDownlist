using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Threading;
using YoutubeDownlist;
using Orthogonal.NTagLite;

namespace YoutubeDownlist
{
    public partial class Form1 : Form
    {
        string listname = "";
        YouTubeAPI ytapi = null;
        string bad = @"\/:*#?<>" + "\"";
        string spliton = "|";

        /*
		Tests:
		// special characters, " - ", TIME title
		https://www.youtube.com/watch?v=VLkJXYyV710
		0:00:00, TIME title
		https://www.youtube.com/watch?v=5N8sUccRiTA
		title TIME
		https://www.youtube.com/watch?v=aItrMzaMxe0
        bad files
        https://www.youtube.com/watch?v=OpzqO-j9r6s&list=PLeiII6jxXbCDdmBqnnfrA-URLrb1Z9-6p&index=1
		list
		https://www.youtube.com/playlist?list=PLeiII6jxXbCDdmBqnnfrA-URLrb1Z9-6p
		*/
        public Form1()
        {
            InitializeComponent();
            if (listHits.LargeImageList == null)
                listHits.LargeImageList = new ImageList();
            listHits.LargeImageList.ColorDepth = ColorDepth.Depth24Bit;
            listHits.LargeImageList.ImageSize = new Size(120, 90);
            ytapi = new YouTubeAPI("AIzaSyBMKFGN-j4pWbuYEWY8h6LYBOWdgOdARj0");
        }

        private void Write(string s)
        {
            this.Invoke((MethodInvoker)delegate
            {
                richOutput.AppendText(s);
                richOutput.ScrollToCaret();
                Console.Write(s);
            });
        }
        private void WriteLine(string s = "")
        {
            Write(s + Environment.NewLine);
        }
        private void WriteFail(string s)
        {
            this.Invoke((MethodInvoker)delegate
            {
                richOutput.SelectionFont = new Font(richOutput.SelectionFont, FontStyle.Bold);
                richOutput.SelectionColor = Color.Red;
            });
            WriteLine("[ FAIL] " + s);
            this.Invoke((MethodInvoker)delegate
            {
                richOutput.SelectionColor = Color.Black;
                richOutput.SelectionFont = new Font(richOutput.SelectionFont, FontStyle.Regular);
            });
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            WriteLine("Started with " + txtUrl.Text);
            Uri srcUri = null;
            if (!Uri.TryCreate(txtUrl.Text, UriKind.Absolute, out srcUri))
            {
                WriteFail("Not a valid uri!");
                return;
            }
            var songs = GetSongs(srcUri);
            foreach (var s in songs)
            {
                ListViewItem li = new ListViewItem();
                li.Text = s.query;
                li.Tag = s;
                s.select += (o, ev) =>
                {
                    var sr = o as ListSearch;
                    if (sr.Selected != null)
                        li.ForeColor = Color.Green;
                    else
                        li.ForeColor = Color.Black;
                };
                listSongs.Items.Add(li);
            }
        }

        private IEnumerable<ListSearch> GetSongs(Uri target)
        {
            string q;
            return GetSongs(target, out q);
        }

        private IEnumerable<ListSearch> GetSongs(Uri target, out string title)
        {
            List<YTVideo> targets = new List<YTVideo>();
            List<ListSearch> slines = new List<ListSearch>();
            if (target.AbsoluteUri.Contains("watch"))
            {
                var vid = ytapi.Video(target);
                title = vid.snippet.title;
                targets.Add(vid);
            }
            else if (target.AbsoluteUri.Contains("playlist"))
            {
                var pl = ytapi.Playlist(target);
                var pli = ytapi.PlaylistItems(target);
                title = pl.snippet.title;
                targets.AddRange(pli.items);
            }
            else
            {
                title = "error";
            }

            foreach (var vid in targets)
            {
                string desc = vid.snippet.description;
                desc = desc.Replace("\r", "");
                var lines = desc.Split('\n');
                Regex reg1, reg2;

                reg1 = new Regex(@"(?:\d{1,2}:)(?:\d{1,2}:?){1,2}");
                reg2 = new Regex(@" ?[\[\(\{]?(?:\d{1,2}:)(?:\d{1,2}:?){1,2}[\]\)\}]?\s?-?\s?");
                string points = "►";

                WriteLine(betterFile(vid.snippet.title));

                
                WriteLine("==========");
                int co = 0;
                foreach (var l in lines)
                {
                    if (reg1.IsMatch(l))
                    {
                        string cleanName = new string(l.Where(c => !points.Contains(c)).ToArray());
                        cleanName = reg2.Replace(cleanName, "");
                        var s = new ListSearch(cleanName);
                        s.album = betterFile(vid.snippet.title);
                        slines.Add(s);
                        co++;
                        WriteLine(cleanName);
                    }
                }
                WriteLine("==========");
                if (co == 0)
                {
                    string cleanName = new string(vid.snippet.title.Where(c => !points.Contains(c)).ToArray());
                    cleanName = reg2.Replace(cleanName, "");
                    slines.Add(new ListSearch(cleanName));
                    continue;
                }

            }
            return slines;
        }

        private void listSongs_DoubleClick(object sender, EventArgs e)
        {
            if (listSongs.SelectedItems.Count == 1)
            {
                ListViewItem si = listSongs.SelectedItems[0];
                listHits.Clear();
                listSongs.Enabled = false;
                string target = (listSongs.SelectedItems[0].Tag as ListSearch).query;
                var results = LookupSong(target);

                foreach (var v in results)
                {
                    var vid = v.snippet;
                    var iconreq = WebRequest.Create(vid.thumbnails.Default.url);

                    listHits.LargeImageList.Images.Add(v.id.videoId, Image.FromStream(iconreq.GetResponse().GetResponseStream()));
                    ListViewItem li = new ListViewItem(vid.title);
                    li.Tag = (MethodInvoker)delegate ()
                    {
                        ListSearch srch = si.Tag as ListSearch;
                        srch.Selected = ytapi.Video(v.id.videoId);
                        listHits.Clear();
                    };
                    li.ImageKey = v.id.videoId;

                    listHits.Items.Add(li);
                }
                listSongs.Enabled = true;
            }
        }

        private IEnumerable<YTSearchResult> LookupSong(string target, int count = 5)
        {
            target = target.Replace("&", "");

            WriteLine("Searching for " + target);
            string q = Uri.EscapeUriString(target);

            return ytapi.Search(target).items;
        }

        private void listHits_DoubleClick(object sender, EventArgs e)
        {
            if (listHits.SelectedItems.Count == 1)
            {
                ListViewItem li = listHits.SelectedItems[0];
                MethodInvoker callback = li.Tag as MethodInvoker;
                callback.Invoke();
            }
        }

        private void btnGo_Click(object sender, EventArgs e)
        {
            btnGo.Enabled = false;
            List<YTVideo> vids = new List<YTVideo>();
            string title = "webm";
            foreach (ListViewItem li in listSongs.Items)
            {
                var s = li.Tag as ListSearch;
                title = s.album;
                if (s.Selected != null)
                    vids.Add(s.Selected);
            }
            new Thread(() =>
            {
                ProcessVids(vids, title);
            }).Start();
        }

        private void OutputHandler(object sender, DataReceivedEventArgs e)
        {
            WriteLine(e.Data);
        }

        private string Download(string id, string dir = "C:\\YoutubeDownlist\\", string fpattern = "00 - % (title)s.% (ext)s", EventHandler exit = null)
        {
            string outname = null;
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = Environment.CurrentDirectory + "\\youtube-dl.exe";
            start.Arguments = "--extract-audio "
            + "--audio-format mp3 "
            + "http://www.youtube.com/watch?v=" + id + " "
            + "-o \"" + dir + fpattern + "\""
            ;
            WriteLine(start.Arguments);
            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.CreateNoWindow = true;
            start.WorkingDirectory = Environment.CurrentDirectory;

            Process process = new Process();
            process.StartInfo = start;
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null && e.Data.Contains("Destination"))
                {
                    outname = e.Data.Substring(e.Data.IndexOf("Destination") + "Destination:".Length).Trim();
                }
                WriteLine(e.Data);
            };
            process.ErrorDataReceived += (s, e) => WriteFail(e.Data);//new DataReceivedEventHandler(OutputHandler);

            if (exit != null)
                process.Exited += exit;

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            while (!process.HasExited)
                Thread.Sleep(100);

            return outname;
        }

        private void Convert(string fname, EventHandler exit = null)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            Process process = new Process();

            if (fname.Contains(".webm"))
                fname = fname.Substring(0, fname.IndexOf(".webm"));
            else if (fname.Contains(".m4a"))
                fname = fname.Substring(0, fname.IndexOf(".m4a"));

            string suffix = "";
            FileInfo fi = new FileInfo(fname + suffix);
            if (!fi.Exists)
                suffix = ".webm";
            fi = new FileInfo(fname + suffix);
            if (!fi.Exists)
                suffix = ".m4a";
            fi = new FileInfo(fname + suffix);

            start = new ProcessStartInfo();
            start.FileName = Environment.CurrentDirectory + "\\ffmpeg.exe";
            start.Arguments = "-i \"" + fname + suffix + "\" "
            + "-hide_banner -acodec libmp3lame -aq 4 -n "
            + "\"" + fname + ".mp3\" "
            ;
            WriteLine(start.Arguments);

            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.CreateNoWindow = true;
            start.WorkingDirectory = Environment.CurrentDirectory;

            process.StartInfo = start;
            process.EnableRaisingEvents = true;

            process.OutputDataReceived += (s, e) => WriteLine(e.Data);
            process.ErrorDataReceived += (s, e) => WriteLine(e.Data);
            if (exit != null)
                process.Exited += exit;

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            while (!process.HasExited)
                Thread.Sleep(100);

            fi.Delete();
        }

        private string betterFile(string fname)
        {
            string ret = fname;
            foreach (var c in spliton)
            {
                if (ret.Contains(c))
                {
                    if (ret.IndexOf(c) >= ret.Length / 2 || ret.Substring(0, ret.IndexOf(c)).Contains(" - "))
                        ret = ret.Substring(0, ret.IndexOf(c));
                    else
                        ret = ret.Substring(ret.IndexOf(c) + 1);
                }
            }

            KeyValuePair<string, string>[] containers = { new KeyValuePair<string, string>("[", "]"), new KeyValuePair<string, string>("【", "】") };
            foreach (var kv in containers)
            {
                string charopen = kv.Key;
                string charclose = kv.Value;

                if (ret.Contains(charopen))
                {
                    if (ret.Contains(charclose))
                    {
                        if (ret.IndexOf(charopen) < ret.IndexOf(charclose))
                        {
                            ret = ret.Substring(0, ret.IndexOf(charopen)) + ret.Substring(ret.IndexOf(charclose) + 1);
                        }
                        else
                        {
                            ret = ret.Replace(charopen, "");
                            ret = ret.Replace(charclose, "");
                        }
                    }
                    else
                    {
                        ret = ret.Replace(charopen, "");
                    }
                }
                else if (ret.Contains(charclose))
                {
                    ret = ret.Replace(charclose, "");
                }
            }

            ret = ret.Replace("Best of ", "");
            ret = new string(ret.Where(c => c > 31 && c < 173 && !bad.Any(l => l == c)).ToArray());
            ret = ret.Trim();
            return ret;
            //return new string(fname.Split('|')[0].Replace("Best of ", "").Replace('|', '_').Where(c => c > 31 && c < 173 && !bad.Any(l => l == c)).ToArray());
        }

        private List<string> ProcessVids(List<YTVideo> vids, string album, string folder = null)
        {
            List<string> ret = new List<string>();
            int i = 0;
            int zeros = (int)Math.Floor(Math.Log10(vids.Count));
            string format = "D" + vids.Count.ToString().Length;

            string dir = Properties.Settings.Default.path;
            if (!Directory.Exists(dir))
            {
                dir = "C:\\YoutubeDownlist";
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }

            if (folder == null)
                folder = album;
            else
                folder += "\\" + album;

            if (!Directory.Exists(dir + "\\" + folder))
                Directory.CreateDirectory(dir + "\\" + folder);

            string m3uloc = dir + "\\" + folder + "\\" + album + ".m3u";
            if (File.Exists(m3uloc))
                File.Delete(m3uloc);

            var playlist = File.CreateText(m3uloc);
            playlist.WriteLine("#EXTM3U");

            foreach (YTVideo vid in vids)
            {
                i++;
                if (vid == null)
                {
                    continue;
                }

                string vname = betterFile(vid.snippet.title);

                string testdir = dir + "\\" + folder + "\\";
                string fname = testdir + i.ToString(format) + " - " + vname.Replace('|', '-');

                if (vid == null)
                {
                    File.WriteAllText(fname + ".txt", "TODO");
                    continue;
                }

                string opt = null;
                FileInfo mf = new FileInfo(fname + ".mp3");
                if (!mf.Exists)
                {
                    string suffix = "";
                    FileInfo fi = new FileInfo(fname + suffix);
                    if (!fi.Exists)
                        suffix = ".webm";
                    fi = new FileInfo(fname + suffix);
                    if (!fi.Exists)
                        suffix = ".m4a";
                    fi = new FileInfo(fname + suffix);
                    
                    if (!fi.Exists)
                        opt = Download(vid.id, testdir, i.ToString(format) + " - " + vname + ".%(ext)s");// i.ToString(format) + " - %(title)s.%(ext)s");

                    if (opt != null)
                    {
                        if (opt != fname + ".webm")
                        {
                            try
                            {
                                File.Copy(opt, fname + ".webm");
                                File.Delete(opt);
                            }
                            catch (Exception e)
                            {
                                this.WriteFail(e.Message + Environment.NewLine + e.StackTrace);
                            }
                        }
                        Convert(fname);
                    }
                    else
                        Convert(fname);
                }
                mf = new FileInfo(fname + ".mp3");
                if (mf.Exists)
                {
                    ret.Add(album + "\\" + i.ToString(format) + " - " + vname + ".mp3");

                    playlist.WriteLine(mf.Name);
                    playlist.Flush();
                    int tries = 0;
                    while (tries >= 0 && tries <= 5)
                    {
                        try
                        {
                            var lf = Orthogonal.NTagLite.LiteFile.LoadFromFile(fname + ".mp3");
                            var tag = lf.Tag;

                            string title = vname;
                            string artist = null;

                            foreach (var sep in new string[] { " - ", " -- ", "- ", " -", " ~ ", " | ", "-", "~", "|"})
                            {
                                if (title.Contains(sep))
                                {
                                    int pos = title.IndexOf(sep);
                                    artist = title.Substring(0, pos);
                                    title = title.Substring(pos + sep.Length);
                                    break;
                                }
                            }

                            tag.Title = title;
                            tag.Album = album;
                            tag.Artist = artist;
                            //int cd = (int)(1 + Math.Floor(i / (double)99));
                            //tag.AddFrame(new Frame())
                            int track = i;
                            int disk = 1;
                            int diskn = (int)Math.Ceiling(vids.Count / (double)99);
                            while (track > 99)
                            {
                                track -= 99;
                                disk++;
                            }
                            tag.TrackNumber = (short?)(track);
                            if (vids.Count > 99)
                                tag.AddTextFrame(FrameId.TPOS, disk + "/" + diskn);

                            Orthogonal.NTagLite.Picture[] pics = lf.Tag.FindFramesById(FrameId.APIC).Select(f => f.GetPicture()).ToArray();
                            var front = pics.SingleOrDefault(p => p.PictureType == LitePictureType.CoverFront);
                            string replaceFile = "NewFrontCover.png";
                            var iconreq = WebRequest.Create(vid.snippet.thumbnails.high.url);
                            Image.FromStream(iconreq.GetResponse().GetResponseStream()).Save(replaceFile);
                            if (front != null)
                            {
                                front.Description = "Front Cover";
                                front.MimeType = LiteHelper.FilenameToImageMime(replaceFile);
                                front.Data = File.ReadAllBytes(replaceFile);
                                front.UpdateSourceFrame();
                                WriteLine("Changed image");
                            }
                            else
                            {
                                lf.Tag.AddPictureFrame(LitePictureType.CoverFront, "Front Cover", replaceFile);
                                WriteLine("Added image");
                            }
                            //File.Delete(replaceFile);
                            lf.UpdateFile();
                            break;
                        }
                        catch (Exception e)
                        {
                            WriteFail(e.GetType().ToString());
                            Thread.Sleep(1000);
                            if (tries >= 5)
                                throw;
                            tries++;
                        }
                    }
                    
                    WriteLine("Completed " + vid.snippet.title);
                }
                else
                {
                    WriteFail(":(");
                }
            }
            playlist.Close();

            this.Invoke((MethodInvoker)delegate () { btnGo.Enabled = true; });
            return ret;
        }

        private void btnAuto_Click(object sender, EventArgs e)
        {
            WriteLine("Started with " + txtUrl.Text);
            Uri srcUri = null;
            if (!Uri.TryCreate(txtUrl.Text, UriKind.Absolute, out srcUri))
            {
                WriteFail("Not a valid uri!");
                return;
            }
            Thread thread = new Thread(() =>
            {
                string masterlist;
                var list = GetSongs(srcUri, out masterlist);
				masterlist = masterlist.Replace("\\", "_").Replace("/", "_");

                string dir = Properties.Settings.Default.path;
                if (!Directory.Exists(dir))
                {
                    dir = "C:\\YoutubeDownlist";
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                }

                if (!Directory.Exists(dir + "\\" + masterlist))
                    Directory.CreateDirectory(dir + "\\" + masterlist);

                //string m3uloc = dir + "\\YTDL\\" + masterlist + ".m3u";
                string m3uloc = dir + "\\" + masterlist + "\\" + masterlist + ".m3u";
                if (File.Exists(m3uloc))
                    File.Delete(m3uloc);

                WriteLine("Masterlist: " + m3uloc);
                var playlist = File.CreateText(m3uloc);
                playlist.WriteLine("#EXTM3U");

                var groups = list.GroupBy(s => s.album);
                foreach (var g in groups)
                {
                    string title = betterFile(g.First().album);
                    List<YTVideo> vids = new List<YTVideo>();
                    foreach (ListSearch search in g)
                    {
                        string target = search.query;
                        var results = LookupSong(target, 1);

                        var vid = ytapi.Video(results.First().id.videoId);
                        vids.Add(vid);
                    }
                    var songnames = ProcessVids(vids, title, masterlist);//, "YTDL");
                    foreach (string s in songnames)
                    {
                        playlist.WriteLine(s);
                        playlist.Flush();
                    }
                }
                playlist.Close();
                WriteLine(">>> FINISHED <<<");
            });
            thread.Start();
        }

        private void btnList_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.InitialDirectory = Environment.CurrentDirectory;
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    Thread thread = new Thread(() =>
                    {
                        var file = new FileInfo(ofd.FileName);
                        string masterlist = file.Name.Replace(file.Extension, "");
                        string[] lines = File.ReadAllLines(ofd.FileName);
                        string dir = Properties.Settings.Default.path;
                        if (!Directory.Exists(dir))
                        {
                            dir = "C:\\YoutubeDownlist";
                            if (!Directory.Exists(dir))
                                Directory.CreateDirectory(dir);
                        }
                        if (!Directory.Exists(dir + "\\" + masterlist))
                            Directory.CreateDirectory(dir + "\\" + masterlist);

                        //string m3uloc = dir + "\\" + masterlist + "\\" + masterlist + ".m3u";
                        //if (File.Exists(m3uloc))
                        //    File.Delete(m3uloc);
                        //WriteLine("Masterlist: " + m3uloc);
                        //var playlist = File.CreateText(m3uloc);
                        //playlist.WriteLine("#EXTM3U");

                        List<ListSearch> list = new List<ListSearch>();
                        foreach (var l in lines)
                        {
                            list.Add(new ListSearch(l));
                        }

                        // TODO: enumerate files and skip those prefixed with i
                        //var files = Directory.EnumerateFiles(dir + "\\" + masterlist + "\\" + masterlist);

                        var groups = list.GroupBy(s => s.album);
                        foreach (var g in groups)
                        {
                            string title = betterFile(g.First().album);
                            List<YTVideo> vids = new List<YTVideo>();
                            int i = 0;
                            foreach (ListSearch search in g)
                            {
                                i++;
                                string target = search.query;
                                var results = LookupSong(target, 5);
                                YTVideo vid = null;
                                string name = search.query.Split('-')[0].Trim();
                                string artist = "";
                                if (search.query.Split('-').Length > 1)
                                    artist = search.query.Split('-')[1].Trim();
                                foreach (var r in results)
                                {
                                    var v = ytapi.Video(r.id.videoId);
                                    if (v != null && v.snippet.title.ToLower().Contains(name.ToLower()) &&
                                        v.snippet.title.ToLower().Contains(artist.ToLower()))
                                    {
                                        vid = v;
                                        break;
                                    }
                                }

                                if (vid == null)
                                {
                                    File.WriteAllText(dir + "\\" + masterlist + "\\" + (i + "").PadLeft(1 + (int)Math.Floor(Math.Log10(g.Count())), '0') + " " + artist + " - " + name + ".txt", "TODO");
                                }

                                // TODO: ensure song has right names
                                //var vid = ytapi.Video(results.First().id.videoId);
                                vids.Add(vid);
                            }
                            var songnames = ProcessVids(vids, masterlist);//, "YTDL");
                            /*
                            foreach (string s in songnames)
                            {
                                playlist.WriteLine(s);
                                playlist.Flush();
                            }*/
                        }
                        //playlist.Close();
                        WriteLine(">>> FINISHED <<<");
                    });
                    thread.Start();
                }
            }
        }
    }

    public class ListSearch
	{
		public ListSearch(string q, EventHandler e = null)
		{
			query = q;
			if (e != null)
				select += e;
		}

		public event EventHandler select;

		public override string ToString()
		{
			return query;
		}

		private ListViewItem li = null;
		public string query = "";
		private YTVideo selected = null;

		public string album = "";

		public YTVideo Selected {
			get
			{
				return selected;
			}
			set
			{
				selected = value;
				if (select != null)
					select.Invoke(this, null);
			}
		}
	}
}

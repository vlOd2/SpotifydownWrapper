using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SpotifydownWrapper.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SpotifydownWrapper
{
    public partial class MainForm : Form
    {
        public const string API_URL = "api.spotifydown.com";
        public const string APPLICATION_NAME = "SpotifydownWrapper";
        public string OutputFolder;

        public MainForm()
        {
            InitializeComponent();
            Text = APPLICATION_NAME;
            Icon = Resources.Logo;
        }

        public async Task<string> RequestAPIURL(string url)
        {
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Origin", "https://spotifydown.com");
            httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
            httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
            httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-site");
            httpClient.DefaultRequestHeaders.Add("Referer", "https://spotifydown.com/");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:108.0) Gecko/20100101 Firefox/108.0");
            return await httpClient.GetStringAsync($"https://{API_URL}/{url}");
        }

        public async Task<byte[]> RequestAudioURL(string url)
        {
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Origin", "https://spotifydown.com");
            httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
            httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
            httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-site");
            httpClient.DefaultRequestHeaders.Add("Referer", "https://spotifydown.com/");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:108.0) Gecko/20100101 Firefox/108.0");
            return await httpClient.GetByteArrayAsync(url);
        }

        public async Task<string[]> GetPlaylistTracks(string playlistID) 
        {
            try
            {
                string request = await RequestAPIURL($"trackList/playlist/{playlistID}");
                List<string> tracks = new List<string>();

                JObject parsedRequest = JsonConvert.DeserializeObject<JObject>(request);
                JArray trackList = (JArray)parsedRequest.GetValue("trackList");

                foreach (JToken token in trackList.ToArray())
                {
                    tracks.Add(token["id"].Value<string>());
                }

                return tracks.ToArray();
            }
            catch (Exception ex) 
            {
                await Task.Run(new Action(() =>
                {
                    MessageBox.Show(
                        $"Unable to get the tracks from the playlist {playlistID}:{Environment.NewLine}" +
                        $"{ex}",
                        $"{APPLICATION_NAME} - Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }));
                return new string[0];
            }
        }

        public async Task<(string, string, string)> GetTrack(string trackID) 
        {
            try 
            {
                string request = await RequestAPIURL($"download/{trackID}");
                JObject parsedRequest = JsonConvert.DeserializeObject<JObject>(request);
                JToken metadata = parsedRequest.GetValue("metadata");
                JToken downloadLink = parsedRequest.GetValue("link");
                return (metadata["title"].Value<string>(), 
                    metadata["artists"].Value<string>(), 
                    downloadLink.Value<string>());
            }
            catch (Exception ex) 
            {
                await Task.Run(new Action(() =>
                {
                    MessageBox.Show(
                        $"Unable to get the download link for the track {trackID}:{Environment.NewLine}" +
                        $"{ex}",
                        $"{APPLICATION_NAME} - Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }));
                return (null, null, null);
            }
        }

        public async Task DownloadTrackAudio(string trackAudioLink, string outputPath) 
        {
            try
            {
                byte[] request = await RequestAudioURL(trackAudioLink);
                FileStream file = new FileStream(outputPath, FileMode.Create);
                await file.WriteAsync(request, 0, request.Length);
                await file.FlushAsync();
                file.Close();
            }
            catch (Exception ex)
            {
                await Task.Run(new Action(() => 
                {
                    MessageBox.Show(
                        $"Unable to download the audio {trackAudioLink}:{Environment.NewLine}" +
                        $"{ex}",
                        $"{APPLICATION_NAME} - Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }));
            }
        }

        private void ResetUI() 
        {
            pbProgress.Maximum = 0;
            pbProgress.Value = 0;
            lStatus.Text = "Not downloading";
            pbStatus.Image = Resources.DownloadStaticEmpty;
            lTracksDone.Text = "0/0";
            btnDownload.Enabled = true;
        }

        private void ResetUIWithError() 
        {
            ResetUI();
            pbProgress.Maximum = 100;
            pbProgress.Value = 100;
            lStatus.Text = "Error";
            pbStatus.Image = Resources.DownloadStaticError;
        }

        private async void btnDownload_Click(object sender, EventArgs e)
        {
            ResetUI();
            string playlistID = txtPlaylistID.Text;
            long startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (string.IsNullOrWhiteSpace(OutputFolder) || !Directory.Exists(OutputFolder))
            {
                await Task.Run(new Action(() =>
                {
                    MessageBox.Show(
                        $"Invalid output folder! Did it get deleted or you didn't choose one?",
                        $"{APPLICATION_NAME} - Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }));
                return;
            }

            if (string.IsNullOrWhiteSpace(playlistID)) 
            {
                await Task.Run(new Action(() =>
                {
                    MessageBox.Show(
                        $"Invalid playlist ID!",
                        $"{APPLICATION_NAME} - Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }));
                return;
            }

            btnDownload.Enabled = false;
            lStatus.Text = $"Fetching tracks from the playlist {playlistID}";
            pbStatus.Image = Resources.DownloadAnimation;
            string[] tracks = await GetPlaylistTracks(playlistID);

            if (tracks.Length < 1) 
            {
                ResetUIWithError();
                return;
            }

            pbProgress.Maximum = tracks.Length * 2 + 25;
            lTracksDone.Text = $"0/{tracks.Length}";
            pbProgress.Value += 25;

            int tracksDone = 0;
            foreach (string track in tracks) 
            {
                lStatus.Text = $"Fetching track {track}";
                (string, string, string) trackInfo = await GetTrack(track);
                string title = trackInfo.Item1;
                string artist = trackInfo.Item2;
                string downloadLink = trackInfo.Item3;

                pbProgress.Value += 1;
                if (title == null || artist == null || downloadLink == null) 
                {
                    pbProgress.Value += 1;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(OutputFolder) || !Directory.Exists(OutputFolder))
                {
                    await Task.Run(new Action(() =>
                    {
                        MessageBox.Show(
                            $"The output folder no longer exists! Did it get deleted?",
                            $"{APPLICATION_NAME} - Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                    }));
                    ResetUIWithError();
                    return;
                }

                lStatus.Text = $"Downloading track {track}";
                if (title.Any(c => Path.GetInvalidFileNameChars().Contains(c))) title = track;
                if (artist.Any(c => Path.GetInvalidFileNameChars().Contains(c))) artist = null;
                await DownloadTrackAudio(downloadLink, $"{OutputFolder}\\{(artist == null ? "" : $"{artist} - ")}{title}.mp3");
                pbProgress.Value += 1;

                tracksDone += 1;
                lTracksDone.Text = $"{tracksDone}/{tracks.Length}";
            }

            long endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            lStatus.Text = $"Downloaded {tracksDone} tracks in {(endTime - startTime) / 1000} seconds";
            pbStatus.Image = Resources.DownloadStaticFilled;
            btnDownload.Enabled = true;
        }

        private void tsmiMenuBarHelpAbout_Click(object sender, EventArgs e)
        {
            new AboutForm().ShowDialog();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            fbdBrowse.ShowDialog();
            OutputFolder = fbdBrowse.SelectedPath;
            txtOutputFolderPath.Text = OutputFolder;
        }
    }
}

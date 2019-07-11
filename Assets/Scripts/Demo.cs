using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Google.Apis.Services;
using Google.Apis.Auth.OAuth2;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using TMPro;

public class Demo : MonoBehaviour
{
    [SerializeField] string streamtitle = "test title";
    [SerializeField] string streamname = "test name";

    [SerializeField] Button openURLBtn;
    [SerializeField] TextMeshProUGUI streamstatusLabel;
    string APP_NAME = "API Sample";

    public void OpenURL()
    {
        Application.OpenURL(streamstatusLabel.text);
    }

    public void OpenStream()
    {
        StartCoroutine(StreamToYoutubeCoroutine());
    }

    IEnumerator StreamToYoutubeCoroutine()
    {
        float dt = .5f;

        //---OAuth2 Authorization
        #region  setup OAuth2 Login with ClientId & ClientSecret
        Debug.Log("======OAuth2 Login Session Started=======");
        streamstatusLabel.text = "setup OAuth2 Login with ClientId & ClientSecret";
        
        Task<UserCredential> taskGetUserCredential = GoogleWebAuthorizationBroker.AuthorizeAsync(
            new ClientSecrets
            {
                ClientId = Credentials.CLIENT_ID,
                ClientSecret = Credentials.CLIENT_SECRET
            },
            new[] { YouTubeService.Scope.Youtube },
            "user", CancellationToken.None);

        yield return new WaitUntil(() => taskGetUserCredential.Status == TaskStatus.RanToCompletion);
        UserCredential credential = taskGetUserCredential.Result;

        Debug.Log($"Login successful with {credential}");

        Debug.Log("======OAuth2 Login Session Ended=======");

        var youtubeService = new YouTubeService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = APP_NAME
        });
        Debug.Log($"Create Youtube Service with user credential {youtubeService.BaseUri}");
        #endregion

        //---create LiveBroadCast
        #region  setup LiveBroadCast Channel (LiveBroadcastSnippet)
        streamstatusLabel.text = "setup LiveBroadCast Channel";
        Debug.Log("======create LiveBroadCast Started=======");
        // Create a snippet with the title and scheduled start and end
        // times for the broadcast. Currently, those times are hard-coded.
        LiveBroadcastSnippet broadcastSnippet = new LiveBroadcastSnippet();
        broadcastSnippet.Title = streamtitle;

        broadcastSnippet.PublishedAt = DateTime.Now;
        broadcastSnippet.ScheduledStartTime = DateTime.Now;
        broadcastSnippet.ScheduledEndTime = DateTime.Now.AddHours(2);

        Debug.Log($"Prepare LiveBroadCast Snippet with Title: {broadcastSnippet.Title}");

        // Set the broadcast's privacy status to "private". See:
        // https://developers.google.com/youtube/v3/live/docs/liveBroadcasts#status.privacyStatus
        LiveBroadcastStatus status = new LiveBroadcastStatus();
        status.PrivacyStatus = "public";

        LiveBroadcast broadcast = new LiveBroadcast();
        broadcast.Kind = "youtube#liveBroadcast";
        broadcast.Snippet = broadcastSnippet;
        broadcast.Status = status;
        Debug.Log($"Prepare LiveBroadCast status: {status.LiveBroadcastPriority}");

        LiveBroadcastContentDetails contentDetails = new LiveBroadcastContentDetails();
        contentDetails.Projection = "360";
        broadcast.ContentDetails = contentDetails;

        Debug.Log($"Try to insert LiveBroadcasts");

        try
        {
            broadcast = youtubeService.LiveBroadcasts.Insert(broadcast, "snippet,status,contentDetails").Execute();
            Debug.Log($"Insert LiveBroadcasts successfully with ID: {broadcast.Id}");
        }
        catch(Exception e)
        {
            Debug.LogError(e);
            yield break;
        }
        Debug.Log("======create LiveBroadCast Ended=======");
        #endregion

        //---create streaming channel
        #region  setup Streaming Channel (LiveStreamSnippet)
        streamstatusLabel.text = "setup Streaming Channel";
        Debug.Log("======create Streaming channel Started=======");
        // Create a snippet with the video stream's title.
        LiveStreamSnippet streamSnippet = new LiveStreamSnippet();
        streamSnippet.Title = streamtitle;

        Debug.Log($"Prepare streaming Snippet {streamSnippet.Title}");

        IngestionInfo ingestionInfo = new IngestionInfo();
        ingestionInfo.StreamName = streamname;

        //this Rtmp address is fixed ,defined by youtube API, 
        //we need to get full push address by appending streaming ID.
        ingestionInfo.IngestionAddress = "rtmp://a.rtmp.youtube.com/live2";

        // Define the content distribution network settings for the
        // video stream. The settings specify the stream's format and
        // ingestion type. See:
        // https://developers.google.com/youtube/v3/live/docs/liveStreams#cdn
        CdnSettings cdnSettings = new CdnSettings();
        cdnSettings.IngestionInfo = ingestionInfo;
        cdnSettings.Format = "720p";
        cdnSettings.IngestionType ="rtmp";

        Debug.Log($"Prepare CDN settings type:{cdnSettings.IngestionType}");

        LiveStream stream = new LiveStream();
        stream.Kind = "youtube#liveStream";
        stream.Snippet = streamSnippet;
        stream.Cdn = cdnSettings;

        // Construct and execute the API request to insert the stream.
        Debug.Log($"Try to insert LiveStreams");
        LiveStream liveStream = null;
        try
        {
            liveStream = youtubeService.LiveStreams.Insert(stream, "snippet,cdn").Execute();
            Debug.Log($"Open livestream successfully with Id {liveStream.Cdn.IngestionInfo.StreamName}");
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            yield break;
        }
        var push_addr = liveStream.Cdn.IngestionInfo.IngestionAddress + "/" + liveStream.Cdn.IngestionInfo.StreamName;
        Debug.Log($"Get rtmp push address: {push_addr}");
        Debug.Log("======create Streaming channel Ended=======");
        #endregion

        //---bind LiveBroadcast & LiveStreams
        #region  bind LiveBroadcast & LiveStreams
        streamstatusLabel.text = "bind LiveBroadcast & LiveStreams";
        Debug.Log("======bind LiveBroadcast & LiveStreams Started=======");
        // Construct and execute a request to bind the new broadcast and stream.
        Debug.Log($"Try to bind LiveBroadcast and streams {broadcast.Id}");
        LiveBroadcastsResource.BindRequest liveBroadcastBind = null;
        try
        {
            liveBroadcastBind = youtubeService.LiveBroadcasts.Bind(broadcast.Id, "id,contentDetails");
            liveBroadcastBind.StreamId = liveStream.Id;
            broadcast = liveBroadcastBind.Execute();
            Debug.Log($"Binding LiveBroadcasts and Streams successfully with Id {liveBroadcastBind.StreamId}");
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            yield break;
        }

        string share_addr = "https://www.youtube.com/watch?v=" + broadcast.Id;
        streamstatusLabel.text = share_addr;
        Debug.Log($"Get user watch address: {share_addr}");
        Debug.Log("======bind LiveBroadcast & LiveStreams Ended=======");
        #endregion

        //---for now, create channel ,get rtmp push addr, get user watch addr jobs are done.
        //---we start to push the streams towards push addr.
        #region  push Stream to rtmp address
        Debug.Log("======push Stream to rtmp address Started=======");

        //TODO : 

        #endregion

        //---switch LiveStream status from Ready to Active
        #region  check LiveStream status if is streaming (ready-->active)
        Debug.Log("======Switch liveBroadcast Ready to Active Started=======");

        var liveStreamlist = youtubeService.LiveStreams.List("id,status");
        liveStreamlist.Id = broadcast.ContentDetails.BoundStreamId;

        LiveStreamListResponse returnedList = liveStreamlist.Execute();
        List<LiveStream> liveStreams = returnedList.Items as List<LiveStream>;
        if (liveStreams != null && liveStreams.Count > 0)
        {
            LiveStream ls = liveStreams[0];

            openURLBtn.interactable = true;

            if (ls != null)
            {
                int errortimes = 0;
                while (errortimes<3 && !ls.Status.StreamStatus.Equals("active"))
                {
                    yield return new WaitForSeconds(dt);
                    Debug.Log($"Pending, current stream status: {ls.Status.StreamStatus} {errortimes}");
                    returnedList = liveStreamlist.Execute();
                    liveStreams = returnedList.Items as List<LiveStream>;
                    liveStream = liveStreams[0];
                    errortimes++;
                }

                if (errortimes >= 3)
                {
                    Debug.Log("Streaming Process is Pending. due to there is no streaming push to youtube address.");
                    errortimes = 0;
                    yield break;
                }
            }    
        }
        Debug.Log("======Switch liveBroadcast Ready to Active Ended=======");
        #endregion

        //---the broadcast can't transition the status from ready to live directly,
        //---it must change to testing first and then live.
        #region transition LiveBroadcasts status (-->testing)
        Debug.Log("======Test Stream Started=======");
        var broadCastTestingRequest = youtubeService.LiveBroadcasts.Transition(LiveBroadcastsResource.TransitionRequest.BroadcastStatusEnum.Testing, broadcast.Id, "id,snippet,contentDetails,status");
        broadcast = broadCastTestingRequest.Execute();

        var liveBroadRequest = youtubeService.LiveBroadcasts.List("id,status");
        liveBroadRequest.BroadcastStatus = LiveBroadcastsResource.ListRequest.BroadcastStatusEnum.All;

        LiveBroadcastListResponse liveBroadcastListResponse = liveBroadRequest.Execute();
        List<LiveBroadcast> broadcastReturnedList = liveBroadcastListResponse.Items as List<LiveBroadcast>;
        if (broadcastReturnedList != null && broadcastReturnedList.Count > 0)
        {
            var  liveBroadcastReq = broadcastReturnedList[0];
            if (liveBroadcastReq != null)
            {
                int errortimes = 0;
                while (errortimes < 3 && !liveBroadcastReq.Status.LifeCycleStatus.Equals("testing"))
                {
                    yield return new WaitForSeconds(dt);
                    Debug.Log("Error publish broadcast - getLifeCycleStatus: " + liveBroadcastReq.Status.LifeCycleStatus);
                    liveBroadcastListResponse = liveBroadRequest.Execute();
                    broadcastReturnedList = liveBroadcastListResponse.Items as List<LiveBroadcast>;
                    liveBroadcastReq = broadcastReturnedList[0];
                    errortimes++;
                }

                if (errortimes >= 3)
                {
                    Debug.Log("Streaming Process is Pending. due to there is problem preforming testing.");
                    errortimes = 0;
                    yield break;
                }
            }  
        }
        Debug.Log("======Test Stream Ended=======");
        #endregion

        //---transition the status from testing to live
        #region transition LiveBroadcasts status (testing-->live)
        var broadCastLiveRequest = youtubeService.LiveBroadcasts.Transition(LiveBroadcastsResource.TransitionRequest.BroadcastStatusEnum.Live, broadcast.Id, "status");
        broadcast = broadCastLiveRequest.Execute();
        #endregion
    }
}

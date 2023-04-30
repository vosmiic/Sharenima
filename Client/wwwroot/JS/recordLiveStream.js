let ovenLiveKit;
var recordLiveStreamdotNetHelper;

function setRecordLiveStreamDotNetHelper(value) {
    recordLiveStreamdotNetHelper = value;
}
function goLive(streamUrl, webcamSlashMic) {
    ovenLiveKit = OvenLiveKit.create({
        callbacks: {
            error: function () {
                recordLiveStreamdotNetHelper.invokeMethodAsync("DisplayStreamingError");
            }
        }
    });

    let mediaStream;
    webcamSlashMic ? ovenLiveKit.getUserMedia().then(function (stream) {
        ovenLiveKit.startStreaming(streamUrl);
        mediaStream = stream;
        onStream(mediaStream);
    }) : ovenLiveKit.getDisplayMedia().then(function (stream) {
        ovenLiveKit.startStreaming(streamUrl);
        mediaStream = stream;
        onStream(mediaStream);
    });
    
    function onStream(stream) {
        recordLiveStreamdotNetHelper.invokeMethodAsync('SwapButtonTheme');
        
        stream.getVideoTracks()[0].addEventListener("ended", function () {
            return stopStreaming();
        })
    }
}

function stopStreaming() {
    ovenLiveKit.stopStreaming();
    ovenLiveKit.remove();
}
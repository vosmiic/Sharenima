using Sharenima.Server.Models;

namespace Sharenima.Server.Helpers; 

public class OnlineVideoHelpers {


    public static async Task<YoutubeVideo?> GetYoutubeVideoInfo(HttpClient httpClient, string youtubeUrl) {
        HttpResponseMessage httpResponseMessage = await httpClient.GetAsync($"https://www.youtube.com/oembed?url={youtubeUrl}&format=json");

        if (!httpResponseMessage.IsSuccessStatusCode) return null;
        YoutubeVideo? youtubeVideo = await httpResponseMessage.Content.ReadFromJsonAsync<YoutubeVideo>();
        return youtubeVideo;
    }
}
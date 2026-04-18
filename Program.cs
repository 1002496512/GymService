
using Newtonsoft.Json.Linq;
using PexelsDotNetSDK.Api;
using PexelsDotNetSDK.Models;
using SerpApi;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GymService
{



    public class Exercise
    {
        [JsonPropertyName("ExerciseName")]
        public string ExerciseName { get; set; }

        [JsonPropertyName("ExerciseDescription")]
        public string ExerciseDescription { get; set; }

        [JsonPropertyName("ExerciseImage")]
        public string ExerciseImage { get; set; }
    }

    public class OpenRouterResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice> Choices { get; set; }
    }

    public class Choice
    {
        [JsonPropertyName("message")]
        public Message Message { get; set; }
    }

    public class Message
    {
        [JsonPropertyName("content")]
        public string Content { get; set; }
    }

    public class FitnessApiService
    {
        HttpClient client;

        public FitnessApiService(HttpClient httpClient)
        {
            client = httpClient;
        }
        private readonly string _apiKey = "API key";
        private readonly string _model = "openrouter/free";
        private readonly string _url = "https://openrouter.ai/api/v1/chat/completions";
        private readonly string imagesApiKey = "G1sYHukfElnBTHjrbcCCdqJVfXt3pXx0xlbboR3ZlWohf4rTpve22mjN";
        private readonly string googleApiKey = "b0962739196d3279a8e27efe6638abadc2768db625ba628bc87ebd6998131126";
        public async Task<List<Exercise>> GetExercisesForMachineAsync(string machineName)
        {
            // הגדרת כותרת האימות
            this.client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            // בניית גוף הבקשה
            // אנו מנחים את המודל ב-System Prompt להחזיר *רק* מערך JSON נקי
            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = @"You are a fitness expert.
                        The user will provide a gym machine name.
                        You MUST respond ONLY with a raw JSON array of objects.
                        Each object must have a 'ExerciseName' (string) ,
                        'ExerciseImage (string)' and 'ExerciseDescription' (string) of an exercises
                        that can be done on this machine.
                        If you dont now any photo of this exercise return an empty string.
                        At least fife exercises.    
                        Do not include markdown code blocks,
                        explanations, or any extra text."
                    },
                    new
                    {
                        role = "user",
                        content = $"Machine: {machineName}"
                    }
                }
            };

            string jsonBody = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(_url, content);
            response.EnsureSuccessStatusCode(); // יזרוק שגיאה אם הבקשה נכשלה

            string responseJson = await response.Content.ReadAsStringAsync();

            // המרת התשובה מהשרת לאובייקט C#
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var apiResponse = JsonSerializer.Deserialize<OpenRouterResponse>(responseJson, options);

            string messageContent = apiResponse?.Choices?[0]?.Message?.Content;

            if (string.IsNullOrWhiteSpace(messageContent))
            {
                return new List<Exercise>();
            }

            // ניקוי של שאריות Markdown (לפעמים מודלים חינמיים מתעקשים להוסיף ```json למרות ההוראות)
            messageContent = messageContent.Trim();
            if (messageContent.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                messageContent = messageContent.Substring(7);
            }
            else if (messageContent.StartsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                messageContent = messageContent.Substring(3);
            }

            if (messageContent.EndsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                messageContent = messageContent.Substring(0, messageContent.Length - 3);
            }

            // המרת ה-JSON של התרגילים חזרה לרשימה של אובייקטים
            var exercises = JsonSerializer.Deserialize<List<Exercise>>(messageContent.Trim(), options);

            return exercises ?? new List<Exercise>();
        }
    }


    internal class Program
    {
        static async Task Main(string[] args)
        {
            HttpClient client = new HttpClient();
            var service = new FitnessApiService(client);
            string machineName = "Leg Press";
            Console.WriteLine($"Exercises for machine: {machineName}\n");
            List<Exercise> legPressExercises = await service.GetExercisesForMachineAsync(machineName);
            foreach (var exercise in legPressExercises)
            {
                Console.WriteLine($"ExerciseName : {exercise.ExerciseName}");
                List<string> videoUrls = await GetGoogleVideos($@"Gym Machine:{machineName},
                                                                  Exercise:{exercise.ExerciseName}");
                foreach (var videoUrl in videoUrls)
                {
                    Console.WriteLine($"Video URL: {videoUrl}");
                    Console.WriteLine($"Description: {exercise.ExerciseDescription}\n");
                }
            }
        }



        static async Task<string> GetImageFromGoogle(string query)
        {
            //string googleApiKey = "b0962739196d3279a8e27efe6638abadc2768db625ba628bc87ebd6998131126";
            //using var client = new HttpClient();

            //string url = $"https://www.googleapis.com/customsearch/v1?key={googleApiKey}&cx=YOUR_CSE_ID&q={Uri.EscapeDataString(query)}&searchType=image&num=1";
            //var response = await client.GetAsync(url);
            ////response.EnsureSuccessStatusCode();
            ////string jsonResponse = await response.Content.ReadAsStringAsync();
            ////using var document = JsonDocument.Parse(jsonResponse);
            ////var imageUrl = document.RootElement.GetProperty("items")[0].GetProperty("link").GetString();
            ////return imageUrl ?? string.Empty;



            string apiKey = "b0962739196d3279a8e27efe6638abadc2768db625ba628bc87ebd6998131126";
            Hashtable ht = new Hashtable();
            ht.Add("engine", "google_images");
            ht.Add("q", query);
            // ht.Add("location", "Austin, Texas, United States");
            ht.Add("google_domain", "google.com");
            ht.Add("hl", "en");
            ht.Add("gl", "us");

            try
            {
                GoogleSearch search = new GoogleSearch(ht, apiKey);
                JObject data = search.GetJson();
                var images_results = data["images_results"];
                if (images_results != null && images_results.HasValues)
                {
                    var firstImage = images_results[0];
                    var imageUrl = firstImage["original"]?.ToString();
                    return imageUrl ?? string.Empty;
                }
                else
                {
                    Console.WriteLine("No image results found.");
                    return string.Empty;
                }
            }
            catch (SerpApiSearchException ex)
            {
                Console.WriteLine("Exception:");
                Console.WriteLine(ex.ToString());
                return string.Empty;
            }

        }


        static async Task<string> GetGoogleVideo(string query)
        {
            //https://serpapi.com/google-videos-api
            String apiKey = "b0962739196d3279a8e27efe6638abadc2768db625ba628bc87ebd6998131126";
            Hashtable ht = new Hashtable();
            ht.Add("engine", "google_videos");
            ht.Add("q", query);

            try
            {
                GoogleSearch search = new GoogleSearch(ht, apiKey);
                JObject data = search.GetJson();
                var video_results = data["video_results"];
                if (video_results != null && video_results.HasValues)
                {
                    var firstVideo = video_results[0];
                    var videoUrl = firstVideo["link"]?.ToString();
                    return videoUrl ?? string.Empty;
                }
                else
                {
                    Console.WriteLine("No video results found.");
                    return string.Empty;
                }
            }
            catch (SerpApiSearchException ex)
            {
                Console.WriteLine("Exception:");
                Console.WriteLine(ex.ToString());
                return string.Empty;
            }
        }

        static async Task<List<string>> GetGoogleVideos(string query)
        {
            //https://serpapi.com/google-videos-api
            String apiKey = "api_key";
            Hashtable ht = new Hashtable();
            ht.Add("engine", "google_videos");
            ht.Add("q", query);

            try
            {
                GoogleSearch search = new GoogleSearch(ht, apiKey);
                JObject data = search.GetJson();
                var video_results = data["video_results"];
                if (video_results != null && video_results.HasValues)
                {
                    List<string> videoUrls = new List<string>();
                    foreach (var video in video_results)
                        videoUrls.Add(video["link"]?.ToString());


                    return videoUrls;
                }

            }
            catch (SerpApiSearchException ex)
            {

            }
            return null;
        }
    }
}

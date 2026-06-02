using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace SpeechToText.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorldCupController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public WorldCupController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost("simulate")]
        public async Task<IActionResult> Simulate([FromBody] WorldCupSimulationRequest request)
        {
            if (request == null || request.Groups == null)
            {
                return BadRequest("Invalid request data.");
            }

            var apiKey = _configuration["Translation:ApiKey"];
            var baseUrl = _configuration["Translation:BaseUrl"] ?? "https://api.deepseek.com";
            var model = _configuration["Translation:Model"] ?? "deepseek-chat";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return BadRequest("DeepSeek API Key is not configured. Please add it to appsettings.json.");
            }

            if (baseUrl.EndsWith("/"))
            {
                baseUrl = baseUrl.Substring(0, baseUrl.Length - 1);
            }
            var apiUrl = $"{baseUrl}/v1/chat/completions";

            // Prepare current matches payload for prompt
            object currentMatchesPayload;
            if (!string.IsNullOrWhiteSpace(request.GroupId))
            {
                var targetGroup = request.Groups.FirstOrDefault(g => g.Id.Equals(request.GroupId, StringComparison.OrdinalIgnoreCase));
                currentMatchesPayload = targetGroup != null ? new List<object> {
                    new {
                        GroupId = targetGroup.Id,
                        Matches = targetGroup.Matches.Select(m => new
                        {
                            m.HomeId,
                            m.AwayId,
                            HomeScore = m.HomeScore?.ToString() ?? "null",
                            AwayScore = m.AwayScore?.ToString() ?? "null"
                        }).ToList()
                    }
                } : new List<object>();
            }
            else
            {
                currentMatchesPayload = request.Groups.Select(g => new
                {
                    GroupId = g.Id,
                    Matches = g.Matches.Select(m => new
                    {
                        m.HomeId,
                        m.AwayId,
                        HomeScore = m.HomeScore?.ToString() ?? "null",
                        AwayScore = m.AwayScore?.ToString() ?? "null"
                    }).ToList()
                }).ToList();
            }

            var matchesJson = JsonSerializer.Serialize(currentMatchesPayload);

            string systemPrompt;
            if (!string.IsNullOrWhiteSpace(request.GroupId))
            {
                systemPrompt = $@"You are a professional football analyst and World Cup simulator.
We are simulating Group {request.GroupId} of the 2026 FIFA World Cup.
The teams in Group {request.GroupId} and their relative strength ratings are:
{GetGroupTeamsPrompt(request.GroupId)}

User's current matches inputs for Group {request.GroupId} (if any) are provided. You MUST respect any non-null/non-empty scores and ONLY simulate the remaining unplayed matches in Group {request.GroupId}. Do NOT overwrite user-defined scores.

Please run the simulation for this group. Determine the scores of all unplayed matches, compute the rankings, and write a brief, exciting Chinese summary (about 150-200 words) summarizing how the matches in this group played out and who qualified.

You MUST write the summary strictly in Simplified Chinese and format it to be highly readable. Use flag emojis (like 🇲🇽 for MEX, 🇰🇷 for KOR, 🇨🇿 for CZE, 🇿🇦 for RSA), bold names, bullet points, and clearly list the final points and results.

Example of the expected group stage summary formatting style:
### Group {request.GroupId} 战况总结
{request.GroupId}组展开了惊心动魄的对决！
* 🇲🇽 **墨西哥** (MEX, 东道主)：以 2 胜 1 平的强势战绩夺得 **7分** 雄踞榜首。2-0 完胜南非、3-2 险胜韩国，尽显华丽进攻与东道主本色。
* 🇰🇷 **韩国** (KOR)：首战 1-1 逼平捷克后，3-0 横扫南非，最终积 **4分** 锁定小组次席。
* 🇨🇿 **捷克** (CZE)：虽以 2-0 击败南非、1-1 逼平墨西哥，但因净胜球劣势积 **2分** 屈居第三，遗憾出局。
* 🇿🇦 **南非** (RSA)：三战皆负积 **0分** 遗憾淘汰。
最终，**墨西哥**与**韩国**携手挺进 32 强淘汰赛！

Return the simulation results strictly as a JSON object matching this schema:
{{
  ""groupMatches"": [
    {{
      ""groupId"": ""{request.GroupId}"",
      ""homeId"": ""TEAM_ID"",
      ""awayId"": ""TEAM_ID"",
      ""homeScore"": 2,
      ""awayScore"": 1
    }},
    ...
  ],
  ""summary"": ""Markdown formatted group review...""
}}
Ensure the output contains ONLY the raw JSON object. Do not wrap in markdown block code (like ```json). Verify that the JSON keys and types are correct.";
            }
            else
            {
                systemPrompt = @"You are a professional football analyst and World Cup simulator.
We are simulating the 2026 FIFA World Cup, which has 48 teams divided into 12 groups (A to L), 4 teams per group.
The teams and their relative strength ratings (representing their skill level, higher means stronger) are:
- Group A: MEX (strength 81, Host), RSA (strength 71), KOR (strength 79), CZE (strength 78)
- Group B: CAN (strength 78, Host), BIH (strength 74), QAT (strength 70), SUI (strength 83)
- Group C: BRA (strength 92), MAR (strength 88), HAI (strength 65), SCO (strength 76)
- Group D: USA (strength 84, Host), PAR (strength 77), AUS (strength 78), TUR (strength 81)
- Group E: GER (strength 89), CUW (strength 62), CIV (strength 80), ECU (strength 82)
- Group F: NED (strength 88), JPN (strength 85), SWE (strength 81), TUN (strength 75)
- Group G: BEL (strength 86), EGY (strength 80), IRN (strength 77), NZL (strength 68)
- Group H: ESP (strength 91), CPV (strength 71), KSA (strength 72), URU (strength 86)
- Group I: FRA (strength 93), SEN (strength 82), IRQ (strength 71), NOR (strength 80)
- Group J: ARG (strength 94, Champion), ALG (strength 79), AUT (strength 81), JOR (strength 68)
- Group K: POR (strength 90), COD (strength 73), UZB (strength 74), COL (strength 85)
- Group L: ENG (strength 92), CRO (strength 84), GHA (strength 76), PAN (strength 71)

Rules of the tournament:
1. Group Stage: 12 groups (A to L). Each group plays a single round-robin (6 matches per group, 72 matches in total).
2. Standings in each group are determined by: Points (3 for win, 1 for draw, 0 for loss) > Goal Difference (GD) > Goals Scored (GS) > Strength.
3. Top 2 teams from each of the 12 groups qualify directly (24 teams).
4. The 12 third-placed teams are compared in a leaderboard (using the same criteria: Points > GD > GS > Strength). The top 8 third-placed teams qualify (8 teams).
5. Total 32 teams advance to the Knockout Stage (Round of 32).
6. The Knockout Stage matches are:
- Left Half:
  - M1: A1 vs 3rd-1 (1st qualified third-place team)
  - M2: L1 vs 3rd-2 (2nd qualified third-place team)
  - M3: C1 vs F2
  - M4: E2 vs I2
  - M5: B1 vs 3rd-3
  - M6: K1 vs 3rd-4
  - M7: J1 vs H2
  - M8: D2 vs G2
- Right Half:
  - M9: E1 vs 3rd-5
  - M10: I1 vs 3rd-6
  - M11: F1 vs C2
  - M12: A2 vs B2
  - M13: D1 vs 3rd-7
  - M14: G1 vs 3rd-8
  - M15: H1 vs J2
  - M16: K2 vs L2
- Round of 16:
  - M17: Winner M1 vs Winner M2
  - M18: Winner M3 vs Winner M4
  - M19: Winner M5 vs Winner M6
  - M20: Winner M7 vs Winner M8
  - M21: Winner M9 vs Winner M10
  - M22: Winner M11 vs Winner M12
  - M23: Winner M13 vs Winner M14
  - M24: Winner M15 vs Winner M16
- Quarterfinals:
  - M25: Winner M17 vs Winner M18
  - M26: Winner M19 vs Winner M20
  - M27: Winner M21 vs Winner M22
  - M28: Winner M23 vs Winner M24
- Semifinals:
  - M29: Winner M25 vs Winner M26
  - M30: Winner M27 vs Winner M28
- Third Place Playoff:
  - M32: Loser M29 vs Loser M30
- Final:
  - M31: Winner M29 vs Winner M30

User's current group stage matches inputs (if any) are provided in the request payload. You MUST respect any non-null/non-empty scores provided by the user and ONLY simulate the remaining matches that have 'null' or empty scores. Do NOT overwrite user-defined scores.

Please run the simulation. Determine the scores of all unplayed group stage matches, compute the group rankings, determine the 32 qualified teams, and simulate the knockout stage match by match (from M1 to M32) using the teams' relative strengths, but allowing occasional realistic upsets.

Write an exciting, professional, and detailed Chinese summary (about 250-400 words) summarizing how the tournament played out (e.g. key highlights, surprising upsets, path of the finalists, and champion's achievement). You MUST write it strictly in Simplified Chinese and use markdown formatting to make it highly readable (use bold text for team names and scores, bullet points, emojis for country flags, and clear headings).

Return the simulation results strictly as a JSON object matching this schema:
{
  ""groupMatches"": [
    {
      ""groupId"": ""A"",
      ""homeId"": ""MEX"",
      ""awayId"": ""RSA"",
      ""homeScore"": 2,
      ""awayScore"": 1
    },
    ...
  ],
  ""qualifiedThirds"": [""teamId1"", ""teamId2"", ...], // exact 8 team IDs of the qualified 3rd-place teams in order of 1st to 8th in the comparison
  ""knockoutWinners"": {
    ""M1"": ""MEX"",
    ""M2"": ""ENG"",
    ...
    ""M32"": ""BRA"",
    ""M31"": ""ARG""
  },
  ""summary"": ""Markdown formatted simulation review...""
}
Ensure the output contains ONLY the raw JSON object. Do not wrap in markdown block code (like ```json). Verify that the JSON keys and types are correct.";
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            client.Timeout = TimeSpan.FromMinutes(3);

            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = $"Here is the current state of matches with user inputs: {matchesJson}" }
                },
                temperature = 0.7,
                response_format = new { type = "json_object" }
            };

            var httpContent = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync(apiUrl, httpContent);
                response.EnsureSuccessStatusCode();

                var responseJsonStr = await response.Content.ReadAsStringAsync();
                using var responseDoc = JsonDocument.Parse(responseJsonStr);
                var choices = responseDoc.RootElement.GetProperty("choices");
                var content = choices[0].GetProperty("message").GetProperty("content").GetString() ?? "";

                content = content.Trim();
                // Strip Markdown fence blocks if present (just in case model ignored the instruction)
                if (content.StartsWith("```"))
                {
                    var lines = content.Split('\n');
                    content = string.Join("\n", lines.Skip(1).Take(lines.Length - 2)).Trim();
                    if (content.EndsWith("```"))
                    {
                        content = content.Substring(0, content.Length - 3).Trim();
                    }
                }

                // Check and parse JSON content
                var simulationResult = JsonSerializer.Deserialize<WorldCupSimulationResponse>(content);
                if (simulationResult == null)
                {
                    throw new Exception("Deserialization returned null.");
                }

                return Ok(simulationResult);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WorldCupController] Simulation call failed: {ex.Message}");
                return StatusCode(500, $"AI Simulation failed: {ex.Message}");
            }
        }

        private string GetGroupTeamsPrompt(string groupId)
        {
            return groupId.ToUpper() switch
            {
                "A" => "- MEX (strength 81, Host), RSA (strength 71), KOR (strength 79), CZE (strength 78)",
                "B" => "- CAN (strength 78, Host), BIH (strength 74), QAT (strength 70), SUI (strength 83)",
                "C" => "- BRA (strength 92), MAR (strength 88), HAI (strength 65), SCO (strength 76)",
                "D" => "- USA (strength 84, Host), PAR (strength 77), AUS (strength 78), TUR (strength 81)",
                "E" => "- GER (strength 89), CUW (strength 62), CIV (strength 80), ECU (strength 82)",
                "F" => "- NED (strength 88), JPN (strength 85), SWE (strength 81), TUN (strength 75)",
                "G" => "- BEL (strength 86), EGY (strength 80), IRN (strength 77), NZL (strength 68)",
                "H" => "- ESP (strength 91), CPV (strength 71), KSA (strength 72), URU (strength 86)",
                "I" => "- FRA (strength 93), SEN (strength 82), IRQ (strength 71), NOR (strength 80)",
                "J" => "- ARG (strength 94, Champion), ALG (strength 79), AUT (strength 81), JOR (strength 68)",
                "K" => "- POR (strength 90), COD (strength 73), UZB (strength 74), COL (strength 85)",
                "L" => "- ENG (strength 92), CRO (strength 84), GHA (strength 76), PAN (strength 71)",
                _ => ""
            };
        }
    }

    // Input DTOs
    public class WorldCupSimulationRequest
    {
        [JsonPropertyName("groups")]
        public List<GroupInput> Groups { get; set; } = new();

        [JsonPropertyName("groupId")]
        public string? GroupId { get; set; }
    }

    public class GroupInput
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("matches")]
        public List<MatchInput> Matches { get; set; } = new();
    }

    public class MatchInput
    {
        [JsonPropertyName("homeId")]
        public string HomeId { get; set; } = string.Empty;

        [JsonPropertyName("awayId")]
        public string AwayId { get; set; } = string.Empty;

        [JsonPropertyName("homeScore")]
        public int? HomeScore { get; set; }

        [JsonPropertyName("awayScore")]
        public int? AwayScore { get; set; }
    }

    // Output DTOs
    public class WorldCupSimulationResponse
    {
        [JsonPropertyName("groupMatches")]
        public List<GroupMatchResult> GroupMatches { get; set; } = new();

        [JsonPropertyName("qualifiedThirds")]
        public List<string> QualifiedThirds { get; set; } = new();

        [JsonPropertyName("knockoutWinners")]
        public Dictionary<string, string> KnockoutWinners { get; set; } = new();

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;
    }

    public class GroupMatchResult
    {
        [JsonPropertyName("groupId")]
        public string GroupId { get; set; } = string.Empty;

        [JsonPropertyName("homeId")]
        public string HomeId { get; set; } = string.Empty;

        [JsonPropertyName("awayId")]
        public string AwayId { get; set; } = string.Empty;

        [JsonPropertyName("homeScore")]
        public int HomeScore { get; set; }

        [JsonPropertyName("awayScore")]
        public int AwayScore { get; set; }
    }
}

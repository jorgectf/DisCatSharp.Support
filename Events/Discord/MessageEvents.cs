﻿using DisCatSharp.Entities;
using DisCatSharp.EventArgs;
using DisCatSharp.Phabricator;
using DisCatSharp.Phabricator.Applications.Maniphest;
using DisCatSharp.Support.Entities.Phabricator;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DisCatSharp.Support.Events.Discord
{
    /// <summary>
    /// The message events.
    /// </summary>
    internal class MessageEvents
    {
        /// <summary>
        /// Fired when a new message gets created.
        /// </summary>
        /// <param name="sender">The discord client aka sender.</param>
        /// <param name="e">The event args.</param>
        public static Task Client_MessageCreated(DiscordClient sender, MessageCreateEventArgs e)
        {
            _ = Task.Run(async() =>
            {
                if (e.Guild != null)
                {
                    Regex reg = new(@"\b(?:https:\/\/(?:bugs\.)?aitsys\.dev\/T(\d{1,4)|(?:T)(\d{1,4}))\b");
                    Match match = reg.Match(e.Message.Content);
                    if (match.Success)
                    {
                        await SearchAndSendTaskAsync(match, e.Message, e.Channel);
                    }
                    else if (e.Channel.Id == 889571019902308393 || e.Channel.Id == 920257691551686666)
                    {
                        await PublishMessagesAsync(sender, e);
                    }
                    else
                    {
                        await Task.FromResult(true);
                    }
                }
            });
            return Task.FromResult(true);
        }

        /// <summary>
        /// Publishes messages in announcement channels.
        /// </summary>
        /// <param name="sender">The discord client aka sender.</param>
        /// <param name="e">The event args.</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private static async Task PublishMessagesAsync(DiscordClient sender, MessageCreateEventArgs e)
        {
            if (e.Message.WebhookMessage)
            {
                if (e.Message.Embeds[0].Title.ToLower().Contains("new commit") || e.Message.Embeds[0].Title.ToLower().Contains("new comment on commit"))
                {
                    try
                    {
                        await e.Channel.CrosspostMessageAsync(e.Message);
                    }
                    catch (Exception ex)
                    {
                        sender.Logger.LogError("Unable to publish message.");
                        sender.Logger.LogError(ex.Message);
                        sender.Logger.LogError(ex.StackTrace);
                    }
                }
            }
        }

        /// <summary>
        /// Searches and sends a task.
        /// </summary>
        /// <param name="match">The match.</param>
        /// <param name="message">The message.</param>
        /// <param name="channel">The channel.</param>
        private static async Task<DiscordMessage> SearchAndSendTaskAsync(Match match, DiscordMessage message, DiscordChannel channel)
        {
            try
            {
                Maniphest m = new(Bot.ConduitClient);
                List<ApplicationEditorSearchConstraint> search = new();
                List<int> ids = new()
                {
                    Convert.ToInt32(match.Groups[2].Value)
                };
                search.Add(new("ids", ids));
                var task = m.Search(null, search).First();
                UserSearch user = null;
                Extended extuser = null;
                if (!string.IsNullOrEmpty(task.Owner))
                {
                    var searchUser = new Dictionary<string, dynamic>();
                    string[] phids = { task.Owner };
                    searchUser.Add("phids", phids);
                    var constraints = new Dictionary<string, dynamic>
                {
                    { "constraints", searchUser }
                };
                    var tdata = Bot.ConduitClient.CallMethod("user.search", constraints);
                    var data = JsonConvert.SerializeObject(tdata);

                    user = JsonConvert.DeserializeObject<UserSearch>(data);
                    var username = new List<string>
                {
                    user.Result.Data[0].Fields.Username
                };

                    var extconstraints = new Dictionary<string, dynamic>
                {
                    { "usernames", username }
                };
                    var tdata2 = Bot.ConduitClient.CallMethod("user.query", extconstraints);
                    var data2 = JsonConvert.SerializeObject(tdata2);
                    extuser = JsonConvert.DeserializeObject<Extended>(data2);
                }
                PhabManiphestTask embed = new(task, user, extuser);
                DiscordMessageBuilder builder = new();
                builder.AddEmbed(embed.GetEmbed());
                return await message.RespondAsync(builder);
            }
            catch (Exception ex)
            {
                return await channel.SendMessageAsync(new DiscordEmbedBuilder()
                {
                    Title = "Error",
                    Description = $"Exception: {ex.Message}\n" +
                    $"```\n" +
                    $"{ex.StackTrace}\n" +
                    $"```"
                });
            }
        }
    }
}

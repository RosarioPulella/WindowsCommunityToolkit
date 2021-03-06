// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.Toolkit.Services.Twitter
{
    /// <summary>
    /// Twitter User type.
    /// </summary>
    public class TwitterStreamDeletedEvent : ITwitterResult
    {
        /// <summary>
        /// Gets or sets the user id of the event. This is always the user who initiated the event.
        /// </summary>
        /// <value>The user Id.</value>
        [JsonPropertyName("user_id_str")]
        public string UserId { get; set; }

        /// <summary>
        /// Gets or sets the id of the event. This is the tweet that was affected.
        /// </summary>
        /// <value>The tweet Id.</value>
        [JsonPropertyName("id_str")]
        public string Id { get; set; }
    }
}

﻿using System.ComponentModel.DataAnnotations;

namespace bazyProjektBlazor.Requests
{
    public class AddNewMessageRequest
    {
        public int ID { get; set; }
        public int MeetingID { get; set; }

        [Required(ErrorMessage = "Message is required")]
        [StringLength(255, ErrorMessage = "Message cannot be longer than 255 characters")]
        public string Message { get; set; } = string.Empty;
    }
}

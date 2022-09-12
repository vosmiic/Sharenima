using System.ComponentModel.DataAnnotations;

namespace Sharenima.Shared; 

public class Permissions {
    public enum Permission {
        [Display(Name = "Administrator")]
        Administrator,
        [Display(Name = "Change video progress")]
        ChangeProgress,
        [Display(Name = "Skip video")]
        SkipVideo,
        [Display(Name = "Add videos")]
        AddVideo,
        [Display(Name = "Upload videos")]
        UploadVideo
    }
}
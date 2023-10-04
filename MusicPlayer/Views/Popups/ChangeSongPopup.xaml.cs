using CommunityToolkit.Maui.Views;

namespace MusicPlayer.Views.Popups;

public partial class ChangeSongPopup: Popup
{
	public ChangeSongPopup()
	{
		InitializeComponent();
	}

    private void Button_Clicked(object sender, EventArgs e)
    {
        Close(new Song { Author = "asd" });

    }

}
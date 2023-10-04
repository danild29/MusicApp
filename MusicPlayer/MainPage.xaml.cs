using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicPlayer.Views.Popups;
using Plugin.Maui.Audio;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace MusicPlayer;


public partial class MainViewMode : ObservableObject
{
    [ObservableProperty] private string message = "";
    [ObservableProperty] private double valueSlider = 0;
    [ObservableProperty] private ObservableCollection<Song> songs = new();

    private readonly IAudioManager audioManager;
    private readonly MainPage mainPage;
    private IAudioPlayer _audio = null;
    private Timer timer;


    public MainViewMode(IAudioManager audioManager, MainPage mainPage)
    {
        this.audioManager = audioManager;
        this.mainPage = mainPage;
        Task.Run(async () =>
        {
            var res = await MusicLoader.LoadAllMusic();
            Songs = new ObservableCollection<Song>(res);
        });

    }

    //private MainViewMode(IAudioManager audioManager, IEnumerable<Song> songs)
    //{
    //    this.audioManager = audioManager;
    //    Songs = new ObservableCollection<Song>(songs);
    //}


    //public async static Task<MainViewMode> Create(IAudioManager audioManager)
    //{
    //    IEnumerable<Song> collection = new List<Song>();
    //    MainViewMode vm = new MainViewMode(audioManager, collection);
    //    return vm;
    //}


    [RelayCommand]
    private void ToggleSong()
    {
        if (_audio == null) return;

        if (_audio.IsPlaying) _audio.Pause();
        else Continue(_audio.CurrentPosition);

    }


    [RelayCommand]
    public async Task ChangeSong(Song song)
    {
        if(song == null) return;


        var res = await mainPage.ShowPopupAsync(new ChangeSongPopup());

    }



    [RelayCommand]
    public void SelectSong(Song choosen)
    {
        if (choosen == null)
            Application.Current.MainPage.DisplayAlert("Error", "song was deleted", "ok");
        else
        {
            Stream stream = MusicLoader.LoadMusicFromDevice(choosen.FileName);
            StartMusic(stream);
        }
    }

    [RelayCommand]
    public async Task SaveNewSong()
    {
        try
        {
            FileResult song = await FilePicker.PickAsync();
            if (song == null) return;

            Stream stream = await MusicLoader.SaveSongAsnyc(song).ConfigureAwait(false);
            StartMusic(stream);

            var music = await MusicLoader.LoadAllMusic();
            Songs = new ObservableCollection<Song>(music);
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    [RelayCommand]
    public void StopMusic() => StopCurrentMusic();

    private void NotifySlider(object state) => ValueSlider = _audio.CurrentPosition / _audio.Duration;

    public void ChangeMusicTime()
    {
        try
        {
            if (_audio == null) return;
            double position = _audio.Duration * ValueSlider;
            Continue(position);
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    private void StartMusic(Stream stream)
    {
        if (_audio != null) StopCurrentMusic();

        _audio = audioManager.CreatePlayer(stream);
        _audio.Loop = true;
        _audio.Play();
        timer = new Timer(NotifySlider, null, 0, 1000);

    }

    public void Pause() => _audio?.Pause();
    public void Continue(double position)
    {
        _audio.Play();
        _audio.Seek(position);
    }

    private void StopCurrentMusic()
    {
        timer.Dispose();
        _audio?.Stop();
        _audio?.Dispose();
        _audio = null;
    }

}





public partial class MainPage : ContentPage
{
    private readonly MainViewMode _vm;

    public MainPage(IAudioManager audioManager)
    {
        InitializeComponent();
        _vm = new MainViewMode(audioManager, this);
        BindingContext = _vm;
    }

    void OnSliderValueChanged(object sender, EventArgs e)
    {
        _vm.Pause();
        _vm.ChangeMusicTime();
    }

    //public async Task Button_Clicked(object sender, EventArgs e)
    //{
    //    var typr = sender.GetType();
    //    await this.ShowPopupAsync(new ChangeSongPopup());
    //}

    //private  TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    //{
    //    var typr = sender.GetType();
    //    await this.ShowPopupAsync(new ChangeSongPopup());
    //}

    private void Button_Clicked(object sender, EventArgs e)
    {
        this.ShowPopup(new ChangeSongPopup());
    }
}



public class Song
{
    public string Name { get; set; }
    public string FileName { get; set; }
    public MusicStatus Status { get; set; } = MusicStatus.None;
    public string Author { get; set; } = "Unknown";
    public string Album { get; set; } = "Unknown";
    public int Duration { get; set; }

    public byte[] Icon { get; set; }
    public Stream Content { get; set; } = null;
}

[Flags]
public enum MusicStatus
{
    None,
    Like,
    Dislike,
    Downloaded,
    Downloading
}


public static class MusicLoader
{

    static string jsonFile = "C:\\Users\\Dania\\source\\repos\\MusicApp\\MusicPlayer\\FilesTxt\\songs.txt";

    private static MusicChangerInfo _jsonWorker = new(jsonFile);

    public static async Task<IEnumerable<Song>> LoadAllMusic()
    {
        var pathDer = FileSystem.AppDataDirectory;
        string[] files = Directory.GetFiles(pathDer);

        var songs = files.Select(x => new Song()
        {
            FileName = x,
            Name = x.Split('\\', StringSplitOptions.TrimEntries).Last()
        }).ToList();


        var infoAboutSongs = await _jsonWorker.GetInfoAboutSongs();

        foreach (var song in songs)
        {
            var info = infoAboutSongs.Where(x => x.FileName == song.FileName).Single();
            song.Status = info.Status;
            song.Name = info.Name;
        }

        return songs;
    }


    public static FileStream LoadMusicFromDevice(string file)
    {
        return File.OpenRead(file);
    }




    public static async Task<Stream> SaveSongAsnyc(FileResult song)
    {
        string localFilePath = Path.Combine(FileSystem.AppDataDirectory, song.FileName);

        if (File.Exists(localFilePath))
            throw new InvalidOperationException("Такая песня уже соранена");


        Action<List<Song>, Song> action = (list, item) =>
        {
            list.Add(item);
        };
        await _jsonWorker.ChangeInfoAboutSongs(action, new Song
        {
            FileName = localFilePath,
            Name = localFilePath.Split('\\', StringSplitOptions.TrimEntries).Last(),
            Status = MusicStatus.Downloaded
        });



        Stream stream = await song.OpenReadAsync();

        using FileStream localFileStream = File.OpenWrite(localFilePath);

        await stream.CopyToAsync(localFileStream);
        return stream;
    }



    public async static Task DeleteSong(string file)
    {
        File.Delete(file);

        Action<List<Song>> action = list => { list.Remove(list.Find(x => x.FileName == file)); };
        await _jsonWorker.ChangeInfoAboutSongs(action);
    }


}
internal class MusicChangerInfo
{

    private string _jsonFile;

    public MusicChangerInfo(string jsonFile)
    {
        _jsonFile = jsonFile;
    }


    public async Task ChangeInfoAboutSongs<T>(Action<List<T>> action)
        => await ChangeInfoAboutSongs(Convert(action), default);


    public async Task ChangeInfoAboutSongs<T>(Action<List<T>, T> action, T song)
    {
        FileStream source = File.Open(_jsonFile, FileMode.Open);
        var infoAboutSongs = (await JsonSerializer.DeserializeAsync<IEnumerable<T>>(source)).ToList();
        source.Dispose();

        action(infoAboutSongs, song);

        string json = JsonSerializer.Serialize(infoAboutSongs);
        await File.WriteAllTextAsync(_jsonFile, json);
    }

    public async Task<IEnumerable<Song>> GetInfoAboutSongs()
    {
        using FileStream source = File.Open(_jsonFile, FileMode.Open);
        return await JsonSerializer.DeserializeAsync<IEnumerable<Song>>(source);
    }




    public static Action<List<T>, T> Convert<T>(Action<List<T>> action)
    {
        if (action == null) return null;

        Action<List<T>, T> convertedAtion = (list, item) => { action(list); };
        return convertedAtion;
    }

}


using Godot;
using System;
using GLADIATE.scripts.audio;
using GLADIATE.scripts.autoloads;

public partial class options_menu : Control
{
    
    SceneLoader _sceneLoader;
    AudioEngine _audioEngine;
    
    HSlider _musicVolumeSlider;
    HSlider _sfxVolumeSlider;
    

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _sceneLoader = GetNode<SceneLoader>("/root/SceneLoader");
        _audioEngine = GetNode<AudioEngine>("/root/audio_engine");
        
        _musicVolumeSlider = GetNode<HSlider>("Control/Music Sliider");
        _sfxVolumeSlider = GetNode<HSlider>("Control/SFX Slider");
        
        _musicVolumeSlider.Value = _audioEngine.GetMusicVolume();
        GD.Print(_audioEngine.GetMusicVolume());
        _sfxVolumeSlider.Value = _audioEngine.GetSfxVolume();
        GD.Print(_audioEngine.GetMusicVolume());

    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        Vector2 originalViewportSize = new Vector2(1920, 1080);
        Vector2 currentViewportSize = GetViewport().GetVisibleRect().Size;
        
        Vector2 scaleFactor = GetViewport().GetVisibleRect().Size / originalViewportSize;
        Vector2 offsetFactor = originalViewportSize - currentViewportSize;

        Scale = scaleFactor;
    }
    
    private void _on_music_sliider_value_changed(double value)
    {
        _audioEngine.SetMusicVolume((float)value);
    }
    
    
    private void _on_sfx_slider_value_changed(double value)
    {
        _audioEngine.SetSfxVolume((float)value);
    }
    
    private void _on_music_mute_button_toggled(bool toggled_on)
    {
        if (toggled_on)
        {
            _audioEngine.MuteMusic();
        }
        else
        {
            _audioEngine.SetMusicVolume((float)_musicVolumeSlider.Value);
        }
    }


    private void _on_sfx_mute_button_toggled(bool toggled_on)
    {
        if (toggled_on)
        {
            _audioEngine.MuteSfx();
        }
        else
        {
            _audioEngine.SetSfxVolume((float)_sfxVolumeSlider.Value);
        }
    }
    
    private void _on_texture_button_pressed()
    {
        OS.ShellOpen("https://github.com/Teicsoft/GLADIATE");
    }
    
    private void _on_credits_button_pressed()
    {
        _sceneLoader.GoToScene("res://scenes/main/Credits.tscn");
        _audioEngine.PlayMusic("Shop_loop_audio.wav");
    }
    
    private void _on_options_button_pressed()
    {
        Show();
    }
    
    private void _on_back_button_pressed()
    {
        Hide();
    }  
    private void _on_screensize_pressed()
    {
        if (DisplayServer.WindowGetMode().Equals(DisplayServer.WindowMode.Fullscreen))
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetSize(new Vector2I(1920, 1080));
        }
        else
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen); 
            
        }
    }
 
}




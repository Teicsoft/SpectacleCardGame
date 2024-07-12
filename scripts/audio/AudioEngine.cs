using System.Collections.Generic;
using System.Threading.Tasks;
using GLADIATE.scripts.customresource;
using Godot;

namespace GLADIATE.scripts.audio;

public partial class AudioEngine : Node
{
	private int _sfxAudioServerIndex = AudioServer.GetBusIndex("SFX");
	private int _musicAudioServerIndex = AudioServer.GetBusIndex("Music");
	private int _peaceAudioServerIndex = AudioServer.GetBusIndex("Voice");
	
	//preloaded audio to avoid stuttering when playing audio for the first time
	private Dictionary<string, AudioStream> _preLoadedMusic = new Dictionary<string, AudioStream>();
	private Dictionary<string, AudioStream> _preLoadedSoundFx = new Dictionary<string, AudioStream>();
	private Dictionary<string, AudioStream> _preLoadedVoiceLines = new Dictionary<string, AudioStream>();

	// two channels for music, to allow for cross fading
	private AudioStreamPlayer _musicPlayer1;
	private AudioStreamPlayer _musicPlayer2;

	// two channels for sound effects, to allow for multiple sounds at once
	private AudioStreamPlayer _soundFxPlayer1;
	private AudioStreamPlayer _soundFxPlayer2;

	// one channel for voice acting as there should only be one voice line at a time for clarity
	private AudioStreamPlayer _voiceLinePlayer;
	private bool _isFadingOut;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_musicPlayer1 = GetNode<AudioStreamPlayer>("MusicPlayer1");
		_musicPlayer2 = GetNode<AudioStreamPlayer>("MusicPlayer2");
		_soundFxPlayer1 = GetNode<AudioStreamPlayer>("SoundFxPlayer1");
		_soundFxPlayer2 = GetNode<AudioStreamPlayer>("SoundFxPlayer2");
		_voiceLinePlayer = GetNode<AudioStreamPlayer>("VoicePlayer");
	}

	// Music Methods //
	public void PlayMusic(string musicFileName)
	{
		AudioStream audioStream = GetAudioStream(musicFileName, AudioType.Music);

		if (!_musicPlayer1.Playing && !_musicPlayer2.Playing) // if nothing is playing, play on channel 1
		{
			_musicPlayer1.Stream = audioStream;
			_musicPlayer1.VolumeDb = 0;
			_musicPlayer1.Play();
		}

		else if (!_musicPlayer1.Playing) // if channel 1 is not playing, but 2 is, cross fade to channel 1
		{
			_musicPlayer1.Stream = audioStream;
			_ = FadeOutTrack(_musicPlayer2);
			_ = FadeInTrack(_musicPlayer1); // assigning Task to discard to make rider happy, as I'm not awaiting async
		}
		else if (!_musicPlayer2.Playing) // if channel 1 is playing, but not 2, cross fade to channel 2
		{
			_musicPlayer2.Stream = audioStream;
			_ = FadeOutTrack(_musicPlayer1);
			_ = FadeInTrack(_musicPlayer2);
		}
		else // if both channels are playing, just stop all music and play on channel 1
		{
			_musicPlayer1.Stop();
			_musicPlayer2.Stop();
			_isFadingOut = false;
			_musicPlayer1.Stream = audioStream;
			_musicPlayer1.VolumeDb = 0;
			_musicPlayer1.Play();
		}
	}

	private async Task FadeOutTrack(AudioStreamPlayer player) // -80 is the lowest volume, 0 is the highest
	{
		if (!_isFadingOut)
		{
			_isFadingOut = true;
			float vol = player.VolumeDb;
			while (vol > -80 && _isFadingOut)
			{
				vol -= 0.1f;
				player.VolumeDb = vol;

				await Task.Delay(5);
				if (vol <= -80)
				{
					_isFadingOut = false;
					player.Stop();
					break;
				}
			}
		}
		else
		{
			player.Stop();
		}
	}

	private async Task FadeInTrack(AudioStreamPlayer player)
	{
		float vol = player.VolumeDb;
		vol = -80f;
		player.VolumeDb = vol;
		player.Play();

		while (vol < 0)
		{
			vol += 1f;
			player.VolumeDb = vol;
			await Task.Delay(15);
			if (vol >= 0)
			{
				break;
			}
		}
	}

	public async Task FadeAllTracks()
	{
		await FadeOutTrack(_musicPlayer1);
		await FadeOutTrack(_musicPlayer2);
	}

	public void StopAllTracks()
	{
		if (_musicPlayer1.Playing)
		{
			_musicPlayer1.Stop();
		}

		if (_musicPlayer2.Playing)
		{
			_musicPlayer2.Stop();
		}
	}

	// Sound Effect Methods //
	public void PlaySoundFx(string soundFxFileName)
	{
		AudioStream audioStream = GetAudioStream(soundFxFileName, AudioType.SoundFx);

		if (!_soundFxPlayer1.Playing) // if nothing is playing, play on channel 1
		{
			_soundFxPlayer1.Stream = audioStream;
			_soundFxPlayer1.Play();
		}
		else if (!_soundFxPlayer2.Playing) // if channel 1 playing, play on 2
		{
			_soundFxPlayer2.Stream = audioStream;
			_soundFxPlayer2.Play();
		}
		else { _ = RetrySoundFx(audioStream); } //otherwise, do nothing, 2 sound effects at once is enough
	}

	private async Task RetrySoundFx(AudioStream audioStream) // to allow for a second effect to be played if close enough to the end of the first line
	{
		await Task.Delay(100); // wait 100ms, if a channel is free, play the sound effect, else skip this one
		if (!_soundFxPlayer1.Playing)
		{
			_soundFxPlayer1.Stream = audioStream;
			_soundFxPlayer1.Play();
		}
		else if (!_soundFxPlayer2.Playing)
		{
			_soundFxPlayer2.Stream = audioStream;
			_soundFxPlayer2.Play();
		}
	}

	public void StopSoundFx()
	{
		_soundFxPlayer1.Stop();
		_soundFxPlayer2.Stop();
	}


	// Voice Acting Methods //
	public void PlayVoiceLine(string voiceLineFileName)
	{
		AudioStream audioStream = GetAudioStream(voiceLineFileName, AudioType.VoiceLine);

		if (!_voiceLinePlayer.Playing) // if nothing is playing, play voice line
		{
			_voiceLinePlayer.Stream = audioStream;
			_voiceLinePlayer.Play();
		}
		else
		{
			_= RetryVoiceLine(audioStream);
		}
	}
	
	private async Task RetryVoiceLine(AudioStream audioStream)
	{
		await Task.Delay(50); // wait 100ms, if channel is free, play the voice line, else give up
		if (!_voiceLinePlayer.Playing)
		{
			_voiceLinePlayer.Stream = audioStream;
			_voiceLinePlayer.Play();
		}
	}


	// General Methods //
	public void StopAllAudio()
	{
		StopAllTracks();
		StopSoundFx();
		_voiceLinePlayer.Stop();
	}

	// declare audio in onReady of scene
	public void PreloadAudio(Dictionary<string, AudioType> sceneAudio)
	{
		foreach (KeyValuePair<string, AudioType> file in sceneAudio)
		{
			AudioStream audioStream = GetAudioStream(file.Key, file.Value);

			switch (file.Value)
			{
				case AudioType.Music:
					if (!_preLoadedMusic.ContainsKey(file.Key))
					{
						_preLoadedMusic.Add(file.Key, audioStream);
					}
					break;
				case AudioType.SoundFx:
					if (!_preLoadedSoundFx.ContainsKey(file.Key))
					{
						_preLoadedSoundFx.Add(file.Key, audioStream);
					}
					break;
				case AudioType.VoiceLine:
					if (!_preLoadedVoiceLines.ContainsKey(file.Key))
					{
						_preLoadedVoiceLines.Add(file.Key, audioStream);
					}
					break;
			}
		}
	}

	public void DestroyPreloadedAudio()
	{
		_preLoadedMusic.Clear();
		_preLoadedSoundFx.Clear();
		_preLoadedVoiceLines.Clear();
	}

	private AudioStream GetAudioStream(string filename, AudioType audioType)
	{
		string folder = null;
		string filePath;

		string musicFolderName = "audio/music/";
		string soundFxFolderName = "audio/sfx/";
		string voiceLineFolderName = "audio/voice/";

		switch (audioType)
		{
			case AudioType.Music:
				if (_preLoadedMusic.ContainsKey(filename))
				{
					return _preLoadedMusic[filename];
				}

				folder = musicFolderName;
				break;

			case AudioType.SoundFx:
				if (_preLoadedSoundFx.ContainsKey(filename))
				{
					return _preLoadedSoundFx[filename];
				}

				folder = soundFxFolderName;
				break;

			case AudioType.VoiceLine:
				if (_preLoadedVoiceLines.ContainsKey(filename))
				{
					return _preLoadedVoiceLines[filename];
				}

				folder = voiceLineFolderName;
				break;
		}

		filePath = ResourceGrabber.GetAssetPath(filename, folder);
		AudioStream audioStream = (AudioStream)ResourceLoader.Load(filePath);
		return audioStream;
	}

	public enum AudioType
	{
		Music,
		SoundFx,
		VoiceLine
	}
	
	public float GetMusicVolume()
	{ return AudioServer.GetBusVolumeDb(_musicAudioServerIndex); }
	public float GetSfxVolume()
	{ return AudioServer.GetBusVolumeDb(_sfxAudioServerIndex); }
	public void SetMusicVolume(float volume)
	{ AudioServer.SetBusVolumeDb(_musicAudioServerIndex, volume); }
	public void SetSfxVolume(float volume)
	{ AudioServer.SetBusVolumeDb(_sfxAudioServerIndex, volume); }
	public void MuteMusic()
	{ AudioServer.SetBusVolumeDb(_musicAudioServerIndex, -80); }
	public void MuteSfx()
	{ AudioServer.SetBusVolumeDb(_sfxAudioServerIndex, -80); }
}

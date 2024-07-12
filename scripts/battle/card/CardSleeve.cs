using Godot;
using GLADIATE.scripts.battle;
using GLADIATE.scripts.battle.card;

public partial class CardSleeve : Control {
    [Signal] public delegate void CardSelectedEventHandler(CardSleeve cardSleeve);

    public Card Card { get; set; }
    public Button SelectButton;
    private TextureRect _art;
    private Vector2 _currentViewportSize;

    //cache
    public Animation Animation { get; set; }
    public AudioStream Sound { get; set; }

    public override void _Ready() {
        SelectButton = GetNode<Button>("SelectButton");
        _art = GetNode<TextureRect>("Art");
        Utils.LoadCardArt(Card, _art);


        GetNode<Label>("Name").Text = Card.CardName;
        GetNode<Label>("Description").Text = Card.Description;
        GetNode<Label>("Background/SpectaclePoints").Text = Card.SpectaclePoints.ToString();
        GetNode<TextureRect>("Background/CardTypeIndicator").Texture =
            (Texture2D)GD.Load($"res://assets/images/Cards/Type Icons/{Card.CardType}.png");

        GetNode<TextureRect>("Background/CardPositionIndicator").Texture =
            (Texture2D)GD.Load($"res://assets/images/Cards/Positon Icons/{Card.TargetPosition}.png");

        GetNode<TextureRect>("Background/cardBG").Texture =
            (Texture2D)GD.Load($"res://assets/images/Cards/cardbgs/{Card.CardType}.png");

        GetNode<Button>("SelectButton").TooltipText = Card.Lore;
        
        _currentViewportSize = GetViewport().GetVisibleRect().Size;
        cardscaling();
    }

    public override void _Process(double delta)
    {
        Vector2 newViewPortSize = GetViewport().GetVisibleRect().Size;
        if (newViewPortSize != _currentViewportSize)
        {
            cardscaling();
            _currentViewportSize = newViewPortSize;
        }
    }

    private void OnPress() { EmitSignal(SignalName.CardSelected, this); }

    public void LoadAssets() {
        Utils.LoadCardArt(Card, _art);
        LoadAnimation();
        LoadSound();
    }

    private void LoadAnimation() { Animation = (Animation)GD.Load(Card.AnimationPath); }

    private void LoadSound() { Sound = (AudioStream)GD.Load(Card.SoundPath); }

    public override string ToString() { return $"CardSleve: " + Card.ToString(); }

    private void cardscaling()
    {
        Vector2 originalViewportSize = new Vector2(1920, 1080);
        Vector2 currentViewportSize = GetViewport().GetVisibleRect().Size;

        float YScale = GetViewport().GetVisibleRect().Size.Y / originalViewportSize.Y;


        Vector2 scaleFactor = new Vector2(
            YScale, YScale
        );
        Scale = scaleFactor;
    }
}
    

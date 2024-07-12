using System;
using System.Collections.Generic;
using System.Linq;
using GLADIATE.scripts.battle.card;
using Godot;

namespace GLADIATE.scripts.battle.target;

public partial class Enemy : Node2D, ITarget {
    [Signal] public delegate void EnemySelectedEventHandler(Enemy enemy);

    public event EventHandler EnemyHealthChangedCustomEvent;
    public event EventHandler EnemyDefenseLowerChangedCustomEvent;
    public event EventHandler EnemyDefenseUpperChangedCustomEvent;

    private Vector2 _currentViewportSize;
    
    public string Id { get; set; }
    public string Name { get; set; }
    public string Image { get; set; }
    public string SoundEffect { get; set; }
    public string Lore { get; set; }
    public string DeckId { get; set; }
    private Utils.ModifierEnum _modifier = Utils.ModifierEnum.None;

    public Color Color;
    public Deck<Card> Deck;
    private int _health;
    private int _defenseUpper = 1;
    private int _defenseLower = 0;
    public int MaxHealth { get; set; }

    private PanelContainer _bossHealthBar;
    public StatusesDecorator Statuses { get; set; }

    public Enemy() { Statuses = new StatusesDecorator { Target = this }; }

    public PanelContainer BossHealthBar {
        get => _bossHealthBar;
        set {
            _bossHealthBar = value;
            UpdateBossHealthBar();
        }
    }
    public Path2D EnemyPath2D { get; set; }
    
    private ColorRect _selectedIndicator;

    private void UpdateBossHealthBar() {
        if (_bossHealthBar == null) return;
        _bossHealthBar.GetNode<Label>("MarginContainer/Control/EnemyName").Text = Name;
        _bossHealthBar.GetNode<ProgressBar>("MarginContainer/Control/Control/HealthProgressBar").Ratio =
            (double)Health / MaxHealth;
        _bossHealthBar.GetNode<Label>("MarginContainer/Control/Control/HealthDisplay").Text =
            GetNode<Label>("HealthBar/HealthDisplay").Text;
        _bossHealthBar.GetNode<Label>("MarginContainer/Control/Control/UpperBlockDisplay").Text =
            DefenseUpper.ToString();
        _bossHealthBar.GetNode<Label>("MarginContainer/Control/Control/LowerBlockDisplay").Text =
            DefenseLower.ToString();
        _bossHealthBar.GetNode<TextureRect>("MarginContainer/Control/Control/ModifierIcon").Texture =
            GetNode<TextureRect>("HealthBar/ModifierIcon").Texture;
        _bossHealthBar.GetNode<TextureRect>("MarginContainer/Control/Control/ModifierIcon").Visible =
            GetNode<TextureRect>("HealthBar/ModifierIcon").Visible;
    }

    public void UpdateStatusesToolTip() {
        TextureRect statusIndicator = GetNode<TextureRect>("HealthBar/StatusIndicator");
        string statusString = "";
        foreach (Utils.StatusEnum status in Statuses) { statusString += status + "\n"; }
        statusIndicator.TooltipText = statusString;
        if (statusString.Length > 0) { statusIndicator.Show(); } else { statusIndicator.Hide(); }
    }

    public Utils.ModifierEnum Modifier {
        get => _modifier;
        set {
            _modifier = value;

            TextureRect icon = GetNode<TextureRect>("HealthBar/ModifierIcon");
            if (value == Utils.ModifierEnum.None) { icon.Visible = false; } else {
                icon.Visible = true;
                icon.Texture = (Texture2D)GD.Load($"res://assets/images/ModifierIcons/{_modifier}.png");
            }

            UpdateBossHealthBar();
        }
    }

    public int Health {
        get => _health;
        set {
            Utils.DirectionEventArgs args = Utils.CheckDirection(_health, value);
            _health = value;
            UpdateHealthBar();
            UpdateBossHealthBar();
            EnemyHealthChangedCustomEvent?.Invoke(this, args);
        }
    }

    public int DefenseLower {
        get => _defenseLower;
        set {
            Utils.DirectionEventArgs args = Utils.CheckDirection(_defenseLower, value);
            _defenseLower = value;
            UpdateDefenseLowerDisplay();
            UpdateBossHealthBar();
            EnemyDefenseLowerChangedCustomEvent?.Invoke(this, args);
        }
    }

    public int DefenseUpper {
        get => _defenseUpper;
        set {
            Utils.DirectionEventArgs args = Utils.CheckDirection(_defenseUpper, value);
            _defenseUpper = value;
            UpdateDefenseUpperDisplay();
            UpdateBossHealthBar();
            EnemyDefenseUpperChangedCustomEvent?.Invoke(this, args);
        }
    }

    public void InitializeEnemy(
        string id, string name, string image, string soundEffect, string lore, string deckId, int maxHealth,
        int defenseUpper, int defenseLower
    ) {
        Id = id;
        Name = name;
        Image = image;
        SoundEffect = soundEffect;
        Lore = lore;
        DeckId = deckId;
        MaxHealth = maxHealth;
        _health = maxHealth;
        _defenseUpper = defenseUpper;
        _defenseLower = defenseLower;
    }

    public void CloneTo(Enemy enemy) {
        enemy.Id = Id;
        enemy.Name = Name;
        enemy.Image = Image;
        enemy.SoundEffect = SoundEffect;
        enemy.Lore = Lore;
        enemy.DeckId = DeckId;
        enemy.MaxHealth = MaxHealth;
        enemy._health = MaxHealth;
        enemy._defenseUpper = DefenseUpper;
        enemy._defenseLower = DefenseLower;
    }

    public override void _Ready() {
        UpdateHealthBar();
        UpdateDefenseUpperDisplay();
        UpdateDefenseLowerDisplay();
        GetNode<Label>("HealthBar/EnemyName").Text = Name;

        GetNode<Sprite2D>("EnemySprite").Texture = (Texture2D)GD.Load(Image);
        GetNode<Button>("SelectButton").TooltipText = Lore;

        _selectedIndicator = GetNode<ColorRect>("SelectedIndicator");
        
        _currentViewportSize = GetViewport().GetVisibleRect().Size;
        enemyscaling();
    }

    public override void _Process(double delta)
    {
        Vector2 newViewPortSize = GetViewport().GetVisibleRect().Size;
        if (newViewPortSize != _currentViewportSize)
        {
            enemyscaling();
            _currentViewportSize = newViewPortSize;
        }
    }

    private void OnJigglePhysicsTimerTimeout() {
        Sprite2D sprite = GetNode<Sprite2D>("EnemySprite");
        sprite.Skew = (float)GD.RandRange(-0.05f, 0.05f);

        if (GD.Randi() % 10 == 0) {
            if (sprite.FlipH) { sprite.FlipH = false; } else { sprite.FlipH = true; }
        }
        
    }

    private void OnPress() { EmitSignal(SignalName.EnemySelected, this); }

    public Card DrawCard() { return Deck.DrawCards(1)[0]; }

    public void TakeCardIntoDiscard(Card card) { Deck.Discard.AddCard(card); }

    public void Damage(int damage, Utils.PositionEnum position) {
        if (!CheckBlock(position)) { DirectDamage(damage); }
    }

    public bool CheckBlock(Utils.PositionEnum position) {
        switch (position) {
            case Utils.PositionEnum.Upper:
                if (DefenseUpper > 0) {
                    DefenseUpper--;
                    return true;
                }

                break;
            case Utils.PositionEnum.Lower:
                if (DefenseLower > 0) {
                    DefenseLower--;
                    return true;
                }

                break;
        }
        return false;
    }

    public void Stun() {
        if (DefenseUpper > 0 || DefenseLower > 0) {
            DefenseUpper = 0;
            DefenseLower = 0;
        } else { Statuses.Add(Utils.StatusEnum.Stunned); }
    }

    public bool IsStunned() {
        bool result = Statuses.Remove(Utils.StatusEnum.Stunned);
        return result;
    }

    public void DirectDamage(int damage) { Health = Math.Max(0, Health - damage); }

    public void Ground(Utils.PositionEnum position) {
        if (!CheckBlock(position)) { Modifier = Utils.ModifierEnum.Grounded; }
    }

    public void Juggle() { Modifier = Utils.ModifierEnum.Juggled; }

    public void Grapple(Utils.PositionEnum position) {
        if (!CheckBlock(position)) { Modifier = Utils.ModifierEnum.Grappled; }
    }

    public void ModifyBlock(int change, Utils.PositionEnum position) {
        switch (position) {
            case Utils.PositionEnum.Upper:
                DefenseUpper = Math.Max(DefenseUpper + change, 0);
                break;
            case Utils.PositionEnum.Lower:
                DefenseLower = Math.Max(DefenseLower + change, 0);
                break;
        }
    }

    private void UpdateHealthBar() {
        GetNode<Label>("HealthBar/HealthDisplay").Text = Health + "/" + MaxHealth;
        GetNode<ProgressBar>("HealthBar/HealthProgressBar").Ratio = (double)Health / MaxHealth;
    }

    private void UpdateDefenseUpperDisplay() {
        GetNode<Label>("HealthBar/UpperBlockDisplay").Text = DefenseUpper.ToString();
    }

    private void UpdateDefenseLowerDisplay() {
        GetNode<Label>("HealthBar/LowerBlockDisplay").Text = DefenseLower.ToString();
    }

    public override string ToString() {
        return
            $"{nameof(Id)}: {Id}, {nameof(Name)}: {Name}, {nameof(Image)}: {Image}, {nameof(SoundEffect)}: {SoundEffect}, {nameof(Lore)}: {Lore}, {nameof(DeckId)}: {DeckId}, {nameof(Modifier)}: {Modifier}, {nameof(Color)}: {Color}, {nameof(Deck)}: {Deck}, {nameof(_health)}: {_health}, {nameof(_defenseUpper)}: {_defenseUpper}, {nameof(_defenseLower)}: {_defenseLower}, {nameof(MaxHealth)}: {MaxHealth}, {nameof(Statuses)}: {Statuses}";
    }

    private void OnCardPlayedTimer() {
        GetNode<Label>("HealthBar/CardPlayed").Visible = false;
        if (_bossHealthBar != null) {
            _bossHealthBar.GetNode<Label>("MarginContainer/Control/CardPlayed").Visible = false;
        }
    }
    private void enemyscaling()
    {
        Vector2 originalViewportSize = new Vector2(1920, 1080);
        Vector2 currentViewportSize = GetViewport().GetVisibleRect().Size;

        float YScale = GetViewport().GetVisibleRect().Size.Y / originalViewportSize.Y;


        Vector2 scaleFactor = new Vector2(
            YScale, YScale
        );
        Scale = scaleFactor;
    }

    public void selectedOn()
    {
        _selectedIndicator.Visible = true;
    }
    
    public void selectedOff()
    {
        _selectedIndicator.Visible = false;
    }
}

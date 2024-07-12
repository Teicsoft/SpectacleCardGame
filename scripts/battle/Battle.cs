using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GLADIATE.scenes.glossary;
using GLADIATE.scripts.audio;
using GLADIATE.scripts.autoloads;
using GLADIATE.scripts.battle.card;
using GLADIATE.scripts.battle.target;
using GLADIATE.scripts.XmlParsing;
using Godot;

namespace GLADIATE.scripts.battle;

public partial class Battle : Control {
    [Signal] public delegate void BattleLostEventHandler();
    [Signal] public delegate void BattleWonEventHandler(Player player);

    [Export] private PackedScene _cardScene;
    [Export] private PackedScene _enemyScene;
    private GameState _gameState;

    private List<TextureRect> _comboArts = new();

    private List<Enemy> _allEnemies;
    private Dictionary<string, List<string>> _allDecks;

    public string Id { get; set; }
    public string BattleName { get; set; }
    public string Music { get; set; }

    private AudioEngine _audioEngine;
    private SceneLoader _sceneLoader;

    private Control _cardGlossary;
    private ComboGlossary _comboGlossary;

    private AnimationPlayer _animation;

    private Vector2 _currentViewportSize;
    
    public override void _Ready() {
        _audioEngine = GetNode<AudioEngine>("/root/audio_engine");
        _sceneLoader = GetNode<SceneLoader>("/root/SceneLoader");

        _allEnemies = EnemyXmlParser.ParseAllEnemies();
        _allDecks = DeckXmlParser.ParseAllDecks();
        _allDecks.TryGetValue(_sceneLoader.DeckSelected, out List<string> playerCardIds);
        
        _animation = GetNode<AnimationPlayer>("AnimationPlayer");
        _animation.Play("RESET");

        Dictionary<string, dynamic> battleData = _sceneLoader.GetCurrentBattleData();
        Id = battleData["battle_id"];
        BattleName = battleData["battle_name"];
        Music = battleData["music"];
        
        _currentViewportSize = GetViewport().GetVisibleRect().Size;
        UIScaling();
        
        List<Enemy> enemies = CreateEnemies((List<string>)battleData["enemies"]);

        InitialiseGameState(playerCardIds, enemies);
        InitialiseHud();

        _comboGlossary = GetNode<ComboGlossary>("Glossary Canvas/ComboGlossary");
        _comboGlossary.Initialize(_gameState.Hand.Deck, _gameState.AllCombos);
        _cardGlossary = GetNode<CardGlossary>("Glossary Canvas/CardGlossary");

        GetNode<Label>("HUD/VsLabel").Text = BattleName;

        if (Id == SceneLoader.BossBattleId) {
            _audioEngine.PlayMusic("Menu_music.wav");
            GD.Print("Boss Battle");

            GetNode<ColorRect>("AspectRatioContainer/Boss red overlay").Show();
        }
        GD.Print(" ==== ==== START GAME ==== ====");
        GD.Print(DisplayServer.WindowGetMode());
    }

    public override void _Process(double delta)
    {
        Vector2 newViewPortSize = GetViewport().GetVisibleRect().Size;
        if (newViewPortSize != _currentViewportSize)
        {
            UIScaling();
            _currentViewportSize = newViewPortSize;
        }

        //this line is needed to prevent a lock situation where both glossaries and escape menu are opened.
        // when the pause menu is closed, the pause state is removed, so glossary stop processing and it is impossible to exit.
        if (_cardGlossary.Visible || _comboGlossary.Visible) { GetTree().Paused = true; }

        if (SceneLoader.BossBattleId == Id) {
            foreach (Enemy enemy in _gameState.Enemies) {
                PathFollow2D enemyPathFollow2D = enemy.EnemyPath2D.GetNode<PathFollow2D>("EnemyLocation");

                if (enemyPathFollow2D.ProgressRatio <= 0.7f) {
                    enemyPathFollow2D.ProgressRatio += 0.5f * (float)delta;

                    enemy.Position = enemyPathFollow2D.Position + _currentViewportSize/2;
                } else if (enemyPathFollow2D.ProgressRatio > 0.7f && enemyPathFollow2D.ProgressRatio <= 1.0f) {
                    if (GD.Randi() % 100 == 0) {
                        enemyPathFollow2D.ProgressRatio += (float)GD.RandRange(-0.3f, 0.3f) * (float)delta * 5;
                        enemy.Position = enemyPathFollow2D.Position + _currentViewportSize/2;
                    }
                } else {
                    enemyPathFollow2D.ProgressRatio = 1.0f;
                    enemy.Position = enemyPathFollow2D.Position + _currentViewportSize/2;
                }
            }
        }
    }

    private void InitialiseGameState(List<string> playerCardIds, List<Enemy> enemies) {
        Hand hand = GetNode<Hand>("Hand");
        hand.InitialiseDeck(playerCardIds);
        _gameState = new GameState(hand, enemies);
        _gameState.Player.PlayerHealthChangedCustomEvent += OnPlayerHealthChanged;
        _gameState.Player.PlayerDefenseLowerChangedCustomEvent += OnPlayerDefenseLowerChanged;
        _gameState.Player.PlayerDefenseUpperChangedCustomEvent += OnPlayerDefenseUpperChanged;

        foreach (Enemy enemy in _gameState.Enemies) {
            enemy.EnemyHealthChangedCustomEvent += OnEnemyHealthChanged;
            enemy.EnemyDefenseLowerChangedCustomEvent += OnEnemyDefenseLowerChanged;
            enemy.EnemyDefenseUpperChangedCustomEvent += OnEnemyDefenseUpperChanged;
        }

        _gameState.SelectedEnemyIndexChangedCustomEvent += MoveSelectedIndicator;
        _gameState.MultiplierChangedCustomEvent += OnMultiplierChanged;
        _gameState.SpectacleChangedCustomEvent += OnSpectacleChanged;
        _gameState.DiscardStateChangedCustomEvent += OnDiscardStateChanged;
        _gameState.ComboStackChangedCustomEvent += OnComboStackChanged;
        _gameState.AllEnemiesDefeatedCustomEvent += WinBattle;
        _gameState.ComboPlayedCustomEvent += DisplayPlayedCombo;
        _gameState.Hand.Deck.DeckShuffledCustomEvent += OnDeckShuffled;
        _gameState.Player.PlayerModifierChangedCustomEvent += OnPlayerModifierChanged;
        _gameState.Player.Statuses.PlayerStatusesChangedCustomEvent += UpdateStatusesToolTip;


        _gameState.Draw(4);
        if (_sceneLoader.Health != 0) { _gameState.Player.Health = _sceneLoader.Health; }
        _sceneLoader.i += 1;
        if (_sceneLoader.SpectaclePoints != 0) { _gameState.SpectaclePoints = _sceneLoader.SpectaclePoints; }
    }

    public void UpdateStatusesToolTip(object sender, EventArgs e) {
        TextureRect statusIndicator = GetNode<TextureRect>("HUD/StatusIndicator");
        string statusString = "";
        foreach (Utils.StatusEnum status in _gameState.Player.Statuses) { statusString += status + "\n"; }
        statusIndicator.TooltipText = statusString;
        if (statusString.Length > 0) { statusIndicator.Show(); } else { statusIndicator.Hide(); }
    }

    private void OnPlayerModifierChanged(object sender, EventArgs e) {
        TextureRect playerModifierIcon = GetNode<TextureRect>("HUD/PlayerModifierIcon");
        if (_gameState.Player.Modifier == Utils.ModifierEnum.None) { playerModifierIcon.Visible = false; } else {
            playerModifierIcon.Texture =
                (Texture2D)GD.Load($"res://assets/images/ModifierIcons/{_gameState.Player.Modifier}.png");
            playerModifierIcon.Visible = true;
        }
    }

    private List<Enemy> CreateEnemies(List<string> enemyIds) {
        int idsCount = enemyIds.Count;
        List<Enemy> enemies = new();
        for (int i = 0; i < idsCount; i++) {
            Enemy enemy = _enemyScene.Instantiate<Enemy>();
            _allEnemies.First(e => e.Id == enemyIds[i]).CloneTo(enemy);

            AssignRandomColorDEBUG(enemy);
            enemy.Deck = GetEnemyDeck(enemy.DeckId);

            Path2D enemyPath2d = GetEnemyPosition(i, idsCount);
            PathFollow2D enemyPathFollow2d = enemyPath2d.GetNode<PathFollow2D>("EnemyLocation");
            enemy.Position = enemyPathFollow2d.Position;
            enemy.EnemyPath2D = enemyPath2d;
            enemy.EnemySelected += MoveSelectedIndicator;

            if (GD.Randi() % 2 == 0) { enemy.GetNode<Sprite2D>("EnemySprite").FlipH = false; }

            if (Id == SceneLoader.PreBossBattleId && enemy.Id == "enemy_Goon") {
                enemy.GetNode<Sprite2D>("EnemySprite").Hide();
                AnimatedSprite2D goon = enemy.GetNode<AnimatedSprite2D>("Goon");
                goon.Show();
                goon.Play("Idle");
                enemy.Scale = new Vector2(1.7f, 1.7f);
                enemy.GetNode<Label>("HealthBar/CardPlayed").Position = new Vector2(100, 0);
            }

            enemyPath2d.AddChild(enemy);
            enemies.Add(enemy);
        }


        if (Id == SceneLoader.BossBattleId) {
            GetNode<PanelContainer>("HUD/BossHealthBarsPanel").Visible = true;

            foreach (Enemy enemy in enemies) {
                enemy.GetNode<Control>("HealthBar").Visible = false;

                PanelContainer bossScene = GD.Load<PackedScene>("res://scenes/battle/boss_health_bars.tscn")
                    .Instantiate<PanelContainer>();
                GetNode<VBoxContainer>("HUD/BossHealthBarsPanel/BossHealthBarsVBoxContainer").AddChild(bossScene);
                enemy.BossHealthBar = bossScene;
            }
        }
        return enemies;
    }

    private static void AssignRandomColorDEBUG(Enemy enemy) {
        switch (GD.Randi() % 2) {
            case 0:
                enemy.Color = new Color(0.5f, 0, 0);
                break;
            case 1:
                enemy.Color = new Color(0, 0.5f, 0);
                break;
            case 2:
                enemy.Color = new Color(0, 0, 0.5f);
                break;
        }
    }

    private Deck<Card> GetEnemyDeck(string deckId) {
        Deck<Card> enemyDeck = new(new());
        _allDecks.TryGetValue(deckId, out List<string> enemyCardIds);
        enemyDeck.AddCards(enemyCardIds.Select(CardFactory.CloneCard).ToList());
        enemyDeck.Shuffle();
        return enemyDeck;
    }

    private Path2D GetEnemyPosition(int index, int count) {
        if (Id == SceneLoader.BossBattleId) {
            GetNode<PanelContainer>("HUD/BossHealthBarsPanel").Visible = true;

            Path2D BossPath2D = GetNode<Path2D>("BossNode/BossBattle" + index);
            PathFollow2D bossLocation = BossPath2D.GetNode<PathFollow2D>("EnemyLocation");

            bossLocation.ProgressRatio = 0;
            bossLocation.Transform = BossPath2D.Transform;

            return BossPath2D;
        }
        Path2D enemyNode = GetNode<Path2D>("Enemies");
        PathFollow2D enemyLocation = enemyNode.GetNode<PathFollow2D>("EnemyLocation");
        enemyLocation.ProgressRatio = (float)index / (count - 1);
        return enemyNode;
    }

    private void InitialiseHud() {
        OnPlayerHealthChanged();
        OnPlayerDefenseUpperChanged();
        OnPlayerDefenseLowerChanged();
        OnMultiplierChanged();
        OnSpectacleChanged();
    }

    private void OnPlayButtonPressed() {
        _gameState.PlaySelectedCard();
        _audioEngine.PlaySoundFx("drawn-card.ogg");
    }

    private void EndTurn() { _gameState.EndTurn(); }

    private void WinBattle(object sender, EventArgs eventArgs) {
        EmitSignal(SignalName.BattleWon, _gameState.Player);

        if (Id != SceneLoader.PreBossBattleId) { _audioEngine.PlaySoundFx("victory-jingle.wav"); } else {
            _audioEngine.PlaySoundFx("male-hurt-1.ogg");
            Task.Delay(10).Wait();
            _audioEngine.PlaySoundFx("male-hurt-2.ogg");
            Task.Delay(10).Wait();
            _audioEngine.PlaySoundFx("male-hurt-3.ogg");
            Task.Delay(10).Wait();
            _audioEngine.PlaySoundFx("male-hurt-4.ogg");
            Task.Delay(10).Wait();
        }

        GD.Print(" ==== ====  WIN BATTLE  ==== ====");
        _sceneLoader.SpectaclePoints += _gameState.SpectaclePoints;
        _sceneLoader.Health = _gameState.Player.Health;
        _sceneLoader.GoToScene("res://scenes/sub/transition.tscn");
    }

    private void MoveSelectedIndicator(Enemy enemy) {
        foreach (Enemy target in _gameState.Enemies)
        {
            target.selectedOff();
        }
        if (_gameState.GetSelectedEnemy() != null)
        {
            enemy.selectedOn();
        }
    }

    private void MoveSelectedIndicator(object sender, EventArgs e) {
        foreach (Enemy target in _gameState.Enemies)
        {
            target.selectedOff();
        }
        if (_gameState.GetSelectedEnemy() != null)
            _gameState.GetSelectedEnemy().selectedOn();
    }

    private void OnPlayerHealthChanged() {
        Player playerObject = _gameState.Player;
        if (playerObject.Health <= 0) {
            EmitSignal(SignalName.BattleLost);

            _sceneLoader = GetNode<SceneLoader>("/root/SceneLoader");
            _audioEngine.PlayMusic("Lil_tune.wav");
            _sceneLoader.GoToScene("res://scenes/sub/GameOver.tscn");
        }
        GetNode<Label>("HUD/PlayerHealthDisplay").Text = playerObject.Health + "/" + playerObject.MaxHealth;
        GetNode<ProgressBar>("HUD/PlayerHealthProgressBar").Ratio =
            (double)playerObject.Health / playerObject.MaxHealth;
    }

    private void OnPlayerDefenseUpperChanged() {
        GetNode<Label>("HUD/PlayerUpperBlockDisplay").Text = _gameState.Player.DefenseUpper.ToString();
    }

    private void OnPlayerDefenseLowerChanged() {
        GetNode<Label>("HUD/PlayerLowerBlockDisplay").Text = _gameState.Player.DefenseLower.ToString();
    }

    private void OnEnemyHealthChanged(object sender, EventArgs e) {
        //todo Particle effect control here
        Utils.DirectionEventArgs directionEventArgs = (Utils.DirectionEventArgs)e;

        // GD.Print("Enemy health went " + directionEventArgs.Direction);
    }

    private void OnEnemyDefenseUpperChanged(object sender, EventArgs e) {
        //todo Particle effect control here
        Utils.DirectionEventArgs directionEventArgs = (Utils.DirectionEventArgs)e;

        // GD.Print("Enemy Upper Defense value went " + directionEventArgs.Direction);
    }

    private void OnEnemyDefenseLowerChanged(object sender, EventArgs e) {
        //todo Particle effect control here
        Utils.DirectionEventArgs directionEventArgs = (Utils.DirectionEventArgs)e;

        // GD.Print("Enemy Lower Defense value went " + directionEventArgs.Direction);
    }

    private void OnMultiplierChanged() {
        GetNode<Label>("HUD/Sp_combo Background/HBoxContainer/MultiplierDisplay").Text =
            _gameState.Multiplier.ToString();
    }

    private void OnSpectacleChanged() {
        GetNode<Label>("HUD/Sp_combo Background/HBoxContainer2/SpectacleDisplay").Text =
            _gameState.SpectaclePoints.ToString();
    }

    private void OnDiscardStateChanged() {
        GetNode<Label>("DiscardDisplay").Text = _gameState.Discards == 0
            ? ""
            : $"You must discard {_gameState.Discards} card{(_gameState.Discards == 1 ? "" : "s")}.";
    }

    private void OnComboStackChanged(object sender, EventArgs e) {
        Path2D comboStack = GetNode<Path2D>("ComboStack");
        PathFollow2D comboStackLocation = GetNode<PathFollow2D>("ComboStack/ComboStackLocation");

        _comboArts.ForEach(art => art.QueueFree());
        _comboArts.Clear();

        int stackSize = _gameState.ComboStack.Count;
        if (stackSize != 0) {
            float stepSize = 1f / Math.Max(stackSize, 7);
            for (int i = 0; i < stackSize; i++) {
                comboStackLocation.ProgressRatio = i * stepSize;
                TextureRect art = Utils.LoadCardArt(_gameState.ComboStack[i]);
                art.Position = comboStackLocation.Position;
                _comboArts.Add(art);
                comboStack.AddChild(art);
            }
        }
    }

    private void DisplayPlayedCombo(object sender, ComboEventArgs e) {
        GetNode<Label>("HUD/ComboDisplay").Text = $"C-C-C-COMBO!!! {e.Combo.Name}!";
        GetNode<Timer>("HUD/ComboDisplay/ComboDisplayTimer").Start();
    }

    private void OnComboDisplayTimeout() { GetNode<Label>("HUD/ComboDisplay").Text = ""; }

    private void OnPlayerHealthChanged(object sender, EventArgs e) {
        Utils.DirectionEventArgs directionEventArgs = (Utils.DirectionEventArgs)e;

        // GD.Print("Player health went " + directionEventArgs.Direction);

        OnPlayerHealthChanged();
    }

    private void OnPlayerDefenseUpperChanged(object sender, EventArgs e) {
        Utils.DirectionEventArgs directionEventArgs = (Utils.DirectionEventArgs)e;

        // GD.Print("Player Upper Defense value went " + directionEventArgs.Direction);

        OnPlayerDefenseUpperChanged();
    }

    private void OnPlayerDefenseLowerChanged(object sender, EventArgs e) {
        Utils.DirectionEventArgs directionEventArgs = (Utils.DirectionEventArgs)e;

        // GD.Print("Player Lower Defense value went " + directionEventArgs.Direction);

        OnPlayerDefenseLowerChanged();
    }

    private void OnMultiplierChanged(object sender, EventArgs e) {
        Utils.DirectionEventArgs directionEventArgs = (Utils.DirectionEventArgs)e;

        // GD.Print("Multiplier value went " + directionEventArgs.Direction);

        OnMultiplierChanged();
    }

    private void OnSpectacleChanged(object sender, EventArgs e) {
        Utils.DirectionEventArgs directionEventArgs = (Utils.DirectionEventArgs)e;

        // GD.Print("Spectacle Points value went " + directionEventArgs.Direction);

        OnSpectacleChanged();
    }

    private void OnDiscardStateChanged(object sender, EventArgs e) { OnDiscardStateChanged(); }
    private void OnDeckShuffled(object sender, EventArgs e) { GD.Print(" Deck Shuffled "); }
    public override string ToString() { return $"Battle: {BattleName}({Id})"; }

    ///
    // This is a quick and dirty patch to give us a scaling UI, also see HUD.cs.
    // Ideally I wouldn't want to define all of the UI elements in code, but just want to make the change fast
    // Further edited to scale only on Yaxis for support of extended aspect ratios
    ///
    private void UIScaling() {
        Vector2 originalViewportSize = new Vector2(1920, 1080);
        Vector2 currentViewportSize = GetViewport().GetVisibleRect().Size;

        float YSCALE = GetViewport().GetVisibleRect().Size.Y / originalViewportSize.Y;
        float XSCALE = (GetViewport().GetVisibleRect().Size.X / originalViewportSize.X);

        Vector2 pathscale = new Vector2(XSCALE, YSCALE);
        

        // float YScale = GetViewport().GetVisibleRect().Size.Y / originalViewportSize.Y;
        Vector2 scaleFactor = new Vector2(
            YSCALE, YSCALE
        ); // XScale and YScale are the same, because we want to keep the aspect ratio
        Vector2 offsetFactor = originalViewportSize - currentViewportSize;

        Path2D hand = GetNode<Path2D>("Hand");
        hand.Curve = adjustcurveX(pathscale,hand.Curve,new Vector2(100,-100),new Vector2(-100,-100));
        
        
        Path2D enemies = GetNode<Path2D>("Enemies");
        enemies.Curve = adjustcurveX(pathscale,enemies.Curve,new Vector2(100,-100),new Vector2(-100,-100));

        Path2D comboStack = GetNode<Path2D>("ComboStack");
        comboStack.Curve = adjustcurveX(pathscale,comboStack.Curve,new Vector2(0,0),new Vector2(0,0));


        Label discardDisplay = GetNode<Label>("DiscardDisplay");
        discardDisplay.Scale = scaleFactor;

        Node2D bossNode = GetNode<Node2D>("BossNode");

        if (Id == SceneLoader.BossBattleId)
        {
            for (int index = 0; index < 6; index++)
            {
                Path2D BossPath2D = GetNode<Path2D>("BossNode/BossBattle" + index);
                BossPath2D.Curve = adjustcurveX(pathscale,BossPath2D.Curve,new Vector2(0,0),new Vector2(0,0));
            }
        }
    }

    private Curve2D adjustcurveX(Vector2 scale, Curve2D curve,Vector2 firstPointAngleOut,Vector2 lastPointAngleIn)
    {
        Vector2[] points = curve.GetBakedPoints();
        Curve2D newCurve2D = new Curve2D();

        points[0] *= scale;
        newCurve2D.AddPoint(points[0],new Vector2(0,0),firstPointAngleOut);
        points[points.Length-1] *= scale;
        newCurve2D.AddPoint(points[points.Length-1],lastPointAngleIn,new Vector2(0,0));
        return newCurve2D;
    }
}

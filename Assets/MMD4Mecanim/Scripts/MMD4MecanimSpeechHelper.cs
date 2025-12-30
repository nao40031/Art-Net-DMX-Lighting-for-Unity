// - Don't upload model data, motion data, this code in github or public space without permission.
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MMD4MecanimSpeechHelper : MMD4MecanimMorphHelper
{
	public AudioClip			speechAudioClip;
	public string				speechMorphText;
	
	public float				elementLength = 0.0f;
	public float				consonantLength = 0.0f;
	
	static readonly float defaultElementLength = 0.2f;
	static readonly float defaultConsonantLength = 0.1f;
	
	public float GetElementLength() { return( elementLength > 0.0f ) ? elementLength : defaultElementLength; }
	public float GetConsonantLength() { return( consonantLength > 0.0f ) ? consonantLength : defaultConsonantLength; }
	
	static List<string> hiraMorphNameList = new List<string>()
	{
		"\u3042", // Hira a
		"\u3044", // Hira i
		"\u3046", // Hira u
		"\u3048", // Hira e
		"\u304A", // Hira o
	};

	// ^ ... alveolar
	// ` ... bilabial
	// / ... staccato
	// - ... -
	// ~ ... -
	
	static bool _IsEscape( char ch )
	{
		return (ch == '^') || (ch == '`') || (ch == '/') || (ch == '-') || (ch == '~');
	}
	
	// 2 or 3
	static Dictionary<string, string> englishPhraseDictionary = new Dictionary<string, string>()
	{
		{"ch",  "i"}, // Reach, Speech
		{"ck",  "u"}, // Kick
		{"cs", "uu"}, // Physics

		{"dy", "i-"}, // Body
		
		{"ff",  "u"}, // Staff

		{"ght", "o"}, // Eight, Knight, Night

		{"kni","ai"}, // Knight

		{"llo","o-"}, // Hello
		
		{"phy", "i"}, // Physics
		
		{"ss", "/u"}, // Lesson
		
		{"ty", "i-"}, // Liberty
		{"th",  "u"}, // Three
		
		{"ca",  "a"}, {"ci",  "i"}, {"cu",  "u"}, {"ce",   "e"}, {"co",   "o"},
		{"qa",  "a"}, {"qi",  "i"}, {"qu",  "u"}, {"qe",   "e"}, {"qo",   "o"},
		{"ka",  "a"}, {"ki",  "i"}, {"ku",  "u"}, {"ke",   "e"}, {"ko",   "o"},
		{"ga",  "a"}, {"gi", "ai"}, {"gu",  "u"}, {"ge",   "e"}, {"go",  "o-"},
		{"sa", "^a"}, {"si", "i/"}, {"su",  "u"}, {"se",  "^u"}, {"so",  "^o"},
		{"sha","^a"}, {"shi", "i"}, {"shu", "u"}, {"she","^i-"}, {"sho", "^o"},
		{"xa", "^a"}, {"xi", "^i"}, {"xu", "^u"}, {"xe",  "^e"}, {"xo",  "^o"},
		{"za", "^a"}, {"zi", "^i"}, {"zu", "^u"}, {"ze",  "^e"}, {"zo",  "^o"},
		{"ja", "^a"}, {"ji",  "i"}, {"ju",  "u"}, {"je",  "^e"}, {"jo",  "^o"},
		{"ta",  "a"}, {"ti",  "i"}, {"tu",  "u"}, {"te",  "^e"}, {"to",  "u-"},
		{"fa", "^a"}, {"fi", "^i"}, {"fu",  "u"}, {"fe",  "^e"}, {"fo",  "^o"},
		{"da",  "a"}, {"di",  "i"}, {"du",  "u"}, {"de",   "e"}, {"do",  "u-"},
		{"na", "^a"}, {"ni", "ai"}, {"nu",  "u"}, {"ne",   "u"}, {"no",  "^o"},
		{"nya","^a"}, {"nyi", "i"}, {"nyu", "u"}, {"nye" , "e"}, {"nyo", "^o"},
		{"ha",  "a"}, {"hi", "ai"}, {"hu",  "u"}, {"he",   "e"}, {"ho",   "o"},
		{"pa", "`a"}, {"pi", "`i"}, {"pu", "`u"}, {"pe",  "`e"}, {"po",  "`o"},
		{"ba", "`a"}, {"bi", "`i"}, {"bu", "`u"}, {"be",   "i"}, {"bo",   "o"},
		{"ma", "`a"}, {"mi", "`i"}, {"mu", "`u"}, {"me",  "`e"}, {"mo",  "`a"},
		{"ya",  "a"}, {"yi",  "i"}, {"yu",  "u"}, {"ye",   "e"}, {"yo",   "o"},
		{"ra",  "o"}, {"ri",  "i"}, {"ru",  "a"}, {"re",   "i"}, {"ro",  "o-"},
		{"la",  "a"}, {"li",  "i"}, {"lu",  "u"}, {"le",   "i"}, {"lo",   "a"},
		{"wa", "^a"}, {"wi","^i-"}, {"wu", "u-"}, {"we", "^i-"}, {"wo", "^o-"},
		{"va", "`a"}, {"vi", "`i"}, {"vu", "`u"}, {"ve",  "`u"}, {"vo",  "`a"},
	};
	
	static Dictionary<char, string> englishPronunDictionary = new Dictionary<char, string>()
	{
		{'a', "a"}, {'i', "i"}, {'u', "u"}, {'e', "e"}, {'o', "o"},
		{'n', "n"},

		{'b', "u"},
		{'c', "u"},
		{'d', "o"},
		{'f', "u"},
		{'g', "u"},
		{'h', "u"},
		{'j', "i"},
		{'k', "u"},
		{'l', "u"},
		{'m', "u"},
		{'p', "u"},
		{'q', "u"},
		{'r', "a"},
		{'s', "u"},
		{'t', "o"},
		{'v', "u"},
		{'w', "u"},
		{'x',"uu"}, // Six
		{'y', "i"},
		{'z', "u"},
	};
	
	static Dictionary<char, string> japanesePostPhraseDictionary = new Dictionary<char, string>()
	{
		{'\u3041',  "a"}, {'\u3043',  "i"}, {'\u3045',  "u"}, {'\u3047',  "e"}, {'\u3049',  "o"}, // Hira la/li/lu/le/lo
		{'\u30A1',  "a"}, {'\u30A3',  "i"}, {'\u30A5',  "u"}, {'\u30A7',  "e"}, {'\u30A9',  "o"}, // Kana la/li/lu/le/lo
	};
	
	// 2
	static Dictionary<string, string> japanesePhraseDictionary = new Dictionary<string, string>()
	{
		{"\u304D\u3041", "^a"}, {"\u304D\u3043", "i"}, {"\u304D\u3045", "u"}, {"\u304D\u3047", "^e"}, {"\u304D\u3049", "^o"}, // Hira kya/kyu/kyo
		{"\u3057\u3041", "^a"}, {"\u3057\u3043", "i"}, {"\u3057\u3045", "u"}, {"\u3057\u3047", "^e"}, {"\u3057\u3049", "^o"}, // Hira sya/shu/sho
		{"\u306B\u3041", "^a"}, {"\u306B\u3043", "i"}, {"\u306B\u3045", "u"}, {"\u306B\u3047", "^e"}, {"\u306B\u3049", "^o"}, // Hira nya/nyu/nyo
		{"\u3072\u3041", "^a"}, {"\u3072\u3043", "i"}, {"\u3072\u3045", "u"}, {"\u3072\u3047", "^e"}, {"\u3072\u3049", "^o"}, // Hira hya/hyu/hyo
		{"\u3073\u3041", "`a"}, {"\u3073\u3043","`i"}, {"\u3073\u3045","`u"}, {"\u3073\u3047", "`e"}, {"\u3073\u3049", "`o"}, // Hira bya/byu/byo
		{"\u3074\u3041", "`a"}, {"\u3074\u3043","`i"}, {"\u3074\u3045","`u"}, {"\u3074\u3047", "`e"}, {"\u3074\u3049", "`o"}, // Hira pya/pyu/byo
		{"\u307E\u3041", "`a"}, {"\u307E\u3043","`i"}, {"\u307E\u3045","`u"}, {"\u307E\u3047", "`e"}, {"\u307E\u3049", "`o"}, // Hira mya/myu/myo
		{"\u308A\u3041", "^a"}, {"\u308A\u3043", "i"}, {"\u308A\u3045", "u"}, {"\u308A\u3047", "^e"}, {"\u308A\u3049", "^o"}, // Hira rya/ryu/ryo

		{"\u30AD\u30A1", "^a"}, {"\u30AD\u30A3", "i"}, {"\u30AD\u30A5", "u"}, {"\u30AD\u30A7", "^e"}, {"\u30AD\u30A9", "^o"}, // Kana kya/nyu/kyo
		{"\u30B7\u30A1", "^a"}, {"\u30B7\u30A3", "i"}, {"\u30B7\u30A5", "u"}, {"\u30B7\u30A7", "^e"}, {"\u30B7\u30A9", "^o"}, // Kana sya/syu/syo
		{"\u30CB\u30A1", "^a"}, {"\u30CB\u30A3", "i"}, {"\u30CB\u30A5", "u"}, {"\u30CB\u30A7", "^e"}, {"\u30CB\u30A9", "^o"}, // Kana nya/nyu/nyo
		{"\u30D2\u30A1", "`a"}, {"\u30D2\u30A3","`i"}, {"\u30D2\u30A5","`u"}, {"\u30D2\u30A7", "`e"}, {"\u30D2\u30A9", "`o"}, // Kana hya/hyu/hyo
		{"\u30D3\u30A1", "`a"}, {"\u30D3\u30A3","`i"}, {"\u30D3\u30A5","`u"}, {"\u30D3\u30A7", "`e"}, {"\u30D3\u30A9", "`o"}, // Kana bya/byu/byo
		{"\u30D4\u30A1", "`a"}, {"\u30D4\u30A3","`i"}, {"\u30D4\u30A5","`u"}, {"\u30D4\u30A7", "`e"}, {"\u30D4\u30A9", "`o"}, // Kana pya/pyu/pyo
		{"\u30DF\u30A1", "`a"}, {"\u30DF\u30A3","`i"}, {"\u30DF\u30A5","`u"}, {"\u30DF\u30A7", "`e"}, {"\u30DF\u30A9", "`o"}, // Kana mya/myu/myo
		{"\u30EA\u30A1", "^a"}, {"\u30EA\u30A3", "i"}, {"\u30EA\u30A5", "u"}, {"\u30EA\u30A7", "^e"}, {"\u30EA\u30A9", "^o"}, // Kana rya/ryu/ryo

		{"\u30F4\u30A1", "`a"}, {"\u30F4\u30A3","`i"}, {"\u30F4\u30A5","`u"}, {"\u30F4\u30A7", "`e"}, {"\u30F4\u30A9", "`o"}, // Kana va/vi/vu/ve/vo
	};
	
	static Dictionary<char, string> punctuatDictionary = new Dictionary<char, string>()
	{
		{ '.', " " },
		{ ',', " " },
		{ '!', " " },
		{ '?', " " },

		{'\uFF0E', " "}, // .
		{'\uFF0C', " "}, // ,
		{'\uFF01', " "}, // !
		{'\uFF1F', " "}, // ?

		{'\u3001', " "}, // Japanese ,
		{'\u3002', " "}, // Japanese .
	};
	
	static Dictionary<char, string> japanesePronunDictionary = new Dictionary<char, string>()
	{
		{'\u30FC', "-"},
		{'\u2015', "-"},
		{'\u301C', "-"},
		
		{'\u3041',  "a"}, {'\u3043',  "i"}, {'\u3045',  "u"}, {'\u3047',  "e"}, {'\u3049',  "o"}, // Hira la/li/lu/le/lo
		{'\u3042',  "a"}, {'\u3044',  "i"}, {'\u3046',  "u"}, {'\u3048',  "e"}, {'\u304A',  "o"}, // Hira a/i/u/e/o
		{'\u304B',  "a"}, {'\u304D',  "i"}, {'\u304F',  "u"}, {'\u3051',  "e"}, {'\u3053',  "o"}, // Hira ka/ki/ku/ke/ko
		{'\u304C',  "a"}, {'\u304E',  "i"}, {'\u3050',  "u"}, {'\u3052',  "e"}, {'\u3054',  "o"}, // Hira ga/gi/gu/ge/go
		{'\u3055', "^a"}, {'\u3057',  "i"}, {'\u3059',  "u"}, {'\u305B', "^e"}, {'\u305D', "^o"}, // Hira sa/si/su/se/so
		{'\u3056', "^a"}, {'\u3058',  "i"}, {'\u305A',  "u"}, {'\u305C', "^e"}, {'\u305E', "^o"}, // Hira za/zi/zu/ze/zo
		{'\u305F', "^a"}, {'\u3061',  "i"}, {'\u3064',  "u"}, {'\u3066', "^e"}, {'\u3068', "^o"}, // Hira ta/chi/tsu/te/to
		{'\u3060', "^a"}, {'\u3062',  "i"}, {'\u3065',  "u"}, {'\u3067', "^e"}, {'\u3069', "^o"}, // Hira da/di/du/de/do
		                                    {'\u3063',  "/"},                                     // Hira ltsu
		{'\u306A', "^a"}, {'\u306B',  "i"}, {'\u306C',  "u"}, {'\u306D', "^e"}, {'\u306E', "^o"}, // Hira na/ni/nu/ne/no
		{'\u306F',  "a"}, {'\u3072',  "i"}, {'\u3075',  "u"}, {'\u3078',  "e"}, {'\u307B',  "o"}, // Hira ha/hi/fu/he/ho
		{'\u3070', "`a"}, {'\u3073', "`i"}, {'\u3076', "`u"}, {'\u3079', "`e"}, {'\u307C', "`o"}, // Hira ba/bi/bu/be/bo
		{'\u3071', "`a"}, {'\u3074', "`i"}, {'\u3077', "`u"}, {'\u307A', "`e"}, {'\u307D', "`o"}, // Hira pa/pi/pu/pe/po
		{'\u307E', "`a"}, {'\u307F', "`i"}, {'\u3080', "`u"}, {'\u3081', "`e"}, {'\u3082', "`o"}, // Hira ma/mi/mu/me/mo
		{'\u3083',  "a"},                   {'\u3085',  "u"},                   {'\u3087',  "o"}, // Hira lya/lyu/lyo
		{'\u3084',  "a"},                   {'\u3086',  "u"},                   {'\u3088',  "o"}, // Hira ya/yu/yo
		{'\u3089', "^a"}, {'\u308A',  "i"}, {'\u308B',  "u"}, {'\u308C', "^e"}, {'\u308D', "^o"}, // Hira ra/ri/ru/re/ro
		{'\u308E',  "a"},                   {'\u3090',  "i"},                   {'\u3092',  "o"}, // Hira lwa/i/wo
		{'\u308F', "^a"},                   {'\u3091',  "e"},                   {'\u3093',  "n"}, // Hira wa/e/n

		{'\u30A1',  "a"}, {'\u30A3',  "i"}, {'\u30A5',  "u"}, {'\u30A7',  "e"}, {'\u30A9',  "o"}, // Kana la/li/lu/le/lo
		{'\u30A2',  "a"}, {'\u30A4',  "i"}, {'\u30A6',  "u"}, {'\u30A8',  "e"}, {'\u30AA',  "o"}, // Kana a/i/u/e/o
		{'\u30AB',  "a"}, {'\u30AD',  "i"}, {'\u30AF',  "u"}, {'\u30B1',  "e"}, {'\u30B3',  "o"}, // Kana ka/ki/ku/ke/ko
		{'\u30AC',  "a"}, {'\u30AE',  "i"}, {'\u30B0',  "u"}, {'\u30B2',  "e"}, {'\u30B4',  "o"}, // Kana ga/gi/gu/ge/go
		{'\u30B5', "^a"}, {'\u30B7',  "i"}, {'\u30B9',  "u"}, {'\u30BB', "^e"}, {'\u30BD', "^o"}, // Kana sa/si/su/se/so
		{'\u30B6', "^a"}, {'\u30B8',  "i"}, {'\u30BA',  "u"}, {'\u30BC', "^e"}, {'\u30BE', "^o"}, // Kana za/zi/zu/ze/zo
		{'\u30BF', "^a"}, {'\u30C1',  "i"}, {'\u30C4',  "u"}, {'\u30C6', "^e"}, {'\u30C8', "^o"}, // Kana ta/chi/tsu/te/to
		{'\u30C0', "^a"}, {'\u30C2',  "i"}, {'\u30C5',  "u"}, {'\u30C7', "^e"}, {'\u30C9', "^o"}, // Kana da/di/du/de/do
		                                    {'\u30C3',  "/"},                                     // Kana ltsu
		{'\u30CA', "^a"}, {'\u30CB',  "i"}, {'\u30CC',  "u"}, {'\u30CD', "^e"}, {'\u30CE', "^o"}, // Kana na/ni/nu/ne/no
		{'\u30CF',  "a"}, {'\u30D2',  "i"}, {'\u30D5',  "u"}, {'\u30D8',  "e"}, {'\u30DB',  "o"}, // Kana ha/hi/fu/he/ho
		{'\u30D0', "`a"}, {'\u30D3', "`i"}, {'\u30D6', "`u"}, {'\u30D9', "`e"}, {'\u30DC', "`o"}, // Kana ba/bi/bu/be/bo
		{'\u30D1', "`a"}, {'\u30D4', "`i"}, {'\u30D7', "`u"}, {'\u30DA', "`e"}, {'\u30DD', "`o"}, // Kana pa/pi/pu/pe/po
		{'\u30DE', "`a"}, {'\u30DF', "`i"}, {'\u30E0', "`u"}, {'\u30E1', "`e"}, {'\u30E2', "`o"}, // Kana ma/mi/mu/me/mo
		{'\u30E3',  "a"},                   {'\u30E5',  "u"},                   {'\u30E7',  "o"}, // Kana lya/lyu/lyo
		{'\u30E4',  "a"},                   {'\u30E6',  "u"},                   {'\u30E8',  "o"}, // Kana ya/yu/yo
		{'\u30E9', "^a"}, {'\u30EA',  "i"}, {'\u30EB',  "u"}, {'\u30EC', "^e"}, {'\u30ED', "^o"}, // Kana ra/ri/ru/re/ro
		{'\u30EE',  "a"},                   {'\u30F0',  "i"},                   {'\u30F2',  "o"}, // Kana lwa/i/wo
		{'\u30EF', "^a"},                   {'\u30F1',  "e"},                   {'\u30F3',  "n"}, // Kana wa/e/n
		{'\u30F4', "`u"},                   {'\u30F5',  "a"},                   {'\u30F6',  "e"}, // Kana vu/lka/lke
	};
	
	struct MorphData
	{
		public char				morphChar;
		public string			morphName;
		public float			morphLength;
	}

	class PlayingData
	{
		public AudioClip		audioClip;
		public float			playingLength;
		public float			playingTime;
		public int				morphPos = -1;
		public float			morphTime;
		public List<MorphData>	morphDataList;
	}

	AudioSource _audioSource;
	List<PlayingData> _playingDataList = new List<PlayingData>();
	int _playingPos = 0;
	bool _isPlayingAudioClip;
	bool _isPlayingMorph;
	
	int _validateMorphBits;
	
	public override bool isProcessing
	{
		get {
			if( this.speechAudioClip != null || !string.IsNullOrEmpty( this.speechMorphText ) ) {
				return true;
			}
			if( _playingDataList != null && _playingPos < _playingDataList.Count ) {
				return true;
			}
			
			return false;
		}
	}
	
	public override bool isAnimating
	{
		get {
			if( base.isAnimating ) {
				return true;
			}
			if( this.speechAudioClip != null || !string.IsNullOrEmpty( this.speechMorphText ) ) {
				return true;
			}
			if( _playingDataList != null && _playingPos < _playingDataList.Count ) {
				return true;
			}
			
			return false;
		}
	}
	
	protected override void Start()
	{
		base.Start();
		if( _model != null ) {
			_audioSource = _model.GetAudioSource();
		}
	}

	protected override void Update()
	{
		_UpdateSpeech( Time.deltaTime );
		base.Update();
	}
	
	public override void ForceUpdate()
	{
		_UpdateSpeech( 0.0f );
		base.ForceUpdate();
	}

	public void ResetSpeech()
	{
		this.speechAudioClip = null;
		this.speechMorphText = "";
		if( _playingDataList.Count > 0 ) {
			if( _isPlayingAudioClip ) {
				_isPlayingAudioClip = false;
				if( _audioSource != null ) {
					_audioSource.Stop();
					_audioSource.clip = null;
				}
			}
			if( _isPlayingMorph ) {
				_isPlayingMorph = false;
				base.morphName = "";
				base.morphWeight = 0.0f;
			}
			_playingDataList.Clear();
			_playingPos = 0;
			ForceUpdate();
		}
	}
	
	void _UpdateSpeech( float deltaTime )
	{
		_UpdatePlayingSpeech();
		
		if( _playingDataList == null || _playingDataList.Count == 0 ) {
			return;
		}
		
		if( _playingPos >= _playingDataList.Count ) {
			if( _isPlayingAudioClip ) {
				_isPlayingAudioClip = false;
				if( _audioSource != null ) {
					_audioSource.Stop();
					_audioSource.clip = null;
				}
			}
			if( _isPlayingMorph ) {
				_isPlayingMorph = false;
				base.morphName = "";
				base.morphWeight = 0.0f;
			}
			_playingDataList.Clear();
			_playingPos = 0;
			return;
		}
		
		PlayingData playingData = _playingDataList[_playingPos];
		while( playingData.playingTime >= playingData.playingLength ) {
			if( _isPlayingAudioClip ) {
				_isPlayingAudioClip = false;
				if( _audioSource != null ) {
					_audioSource.Stop();
					_audioSource.clip = null;
				}
			}
			float paddingTime = playingData.playingTime - playingData.playingLength;
			if( ++_playingPos >= _playingDataList.Count ) {
				if( _isPlayingMorph ) {
					_isPlayingMorph = false;
					base.morphName = "";
					base.morphWeight = 0.0f;
				}
				return;
			}
			playingData = _playingDataList[_playingPos];
			playingData.playingTime = paddingTime;
		}
		
		if( playingData.morphPos < 0 ) {
			playingData.morphPos = 0;
			if( playingData.audioClip != null ) {
				_isPlayingAudioClip = true;
				if( _audioSource != null ) {
					_audioSource.clip = playingData.audioClip;
					_audioSource.Play();
				}
			}
			if( playingData.morphDataList != null && playingData.morphPos < playingData.morphDataList.Count ) {
				_isPlayingMorph = true;
				_UpdateMorph( playingData.morphDataList[playingData.morphPos].morphName );
			} else {
				if( _isPlayingMorph ) {
					_isPlayingMorph = false;
					base.morphName = "";
					base.morphWeight = 0.0f;
				}
			}
		}
		
		bool overrayPlayingTime = false;
		if( _isPlayingAudioClip ) {
			if( _audioSource != null ) {
				if( _audioSource.isPlaying ) {
					playingData.playingTime = _audioSource.time;
					overrayPlayingTime = true;
				} else {
					if( playingData.playingTime < playingData.playingLength ) {
						playingData.playingTime = playingData.playingLength;
						overrayPlayingTime = true;
					}
				}
			}
		}

		if( playingData.morphTime < playingData.playingTime ) {
			if( playingData.morphDataList != null ) {
				float addingTime = playingData.playingTime - playingData.morphTime;
				int beforePos = playingData.morphPos;
				for( ; playingData.morphPos < playingData.morphDataList.Count; ++playingData.morphPos ) {
					float morphLength = playingData.morphDataList[playingData.morphPos].morphLength;
					if( morphLength >= addingTime ) {
						break;
					}
					playingData.morphTime += morphLength;
					addingTime -= morphLength;
				}
				if( beforePos != playingData.morphPos && playingData.morphPos < playingData.morphDataList.Count ) {
					_UpdateMorph( playingData.morphDataList[playingData.morphPos].morphName );
				}
			}
		}
		
		if( !overrayPlayingTime ) {
			playingData.playingTime += deltaTime;
		}
	}
	
	void _UpdateMorph( string morphName )
	{
		if( _validateMorphBits == 0 ) { // Collect enable morph.
			MMD4MecanimModel model = GetComponent<MMD4MecanimModel>();
			if( model != null && hiraMorphNameList != null ) {
				for( int i = 0; i < hiraMorphNameList.Count; ++i ) {
					if( model.GetMorph( hiraMorphNameList[i] ) != null ) {
						_validateMorphBits |= 1 << i;
					}
				}
			}
			if( _validateMorphBits == 0 ) {
				_validateMorphBits = 0x1f;
			}
		}
		
		float morphWeight = 0.0f;
		if( !string.IsNullOrEmpty( morphName ) ) { // Emulation morph using 'a'
			morphWeight = 1.0f;
			if( hiraMorphNameList != null ) {
				if( morphName == hiraMorphNameList[1] && ( ( _validateMorphBits & 0x2 ) == 0 ) ) {
					morphName = hiraMorphNameList[0];
					morphWeight = 0.5f;
				}
				else if( morphName == hiraMorphNameList[2] && ( ( _validateMorphBits & 0x4 ) == 0 ) ) {
					morphName = hiraMorphNameList[0];
					morphWeight = 0.3f;
				}
				else if( morphName == hiraMorphNameList[3] && ( ( _validateMorphBits & 0x8 ) == 0 ) ) {
					morphName = hiraMorphNameList[0];
					morphWeight = 0.5f;
				}
				else if( morphName == hiraMorphNameList[4] && ( ( _validateMorphBits & 0x10 ) == 0 ) ) {
					morphName = hiraMorphNameList[0];
					morphWeight = 0.8f;
				}
			}
		}
		
		base.morphName = morphName;
		base.morphWeight = morphWeight;
	}
	
	void _UpdatePlayingSpeech()
	{
		if( this.speechAudioClip != null || !string.IsNullOrEmpty( this.speechMorphText ) ) {
			PlayingData playingData = _ParsePlayingData( this.speechAudioClip, this.speechMorphText );
			this.speechAudioClip = null;
			this.speechMorphText = "";

			_playingDataList.Clear();
			_playingPos = 0;
			if( _isPlayingAudioClip ) {
				_isPlayingAudioClip = false;
				if( _audioSource != null ) {
					_audioSource.Stop();
					_audioSource.clip = null;
				}
			}
			if( playingData != null ) {
				_playingDataList.Add( playingData );
			}
		}
	}
	
	PlayingData _ParsePlayingData( AudioClip audioClip, string morphText )
	{
		PlayingData playingData		= new PlayingData();
		playingData.audioClip		= audioClip;
		playingData.playingLength	= 0.0f;
		playingData.playingTime		= 0.0f;
		playingData.morphPos		= -1;
		playingData.morphTime		= 0.0f;
		if( !string.IsNullOrEmpty( morphText ) ) {
			playingData.morphDataList = _ParseMorphText( morphText );
		} else {
			playingData.morphDataList = _ParseMorphText( System.IO.Path.GetFileNameWithoutExtension( audioClip.name ) );
		}
		
		if( playingData.morphDataList != null ) {
			float morphTotalLength = 0.0f;
			int morphZeroCount = 0;
			for( int i = 0; i < playingData.morphDataList.Count; ++i ) {
				char ch = playingData.morphDataList[i].morphChar;
				if( playingData.morphDataList[i].morphLength == 0.0f ) {
					if( ch != '^' && ch != '`' ) {
						++morphZeroCount;
					}
				} else {
					morphTotalLength += playingData.morphDataList[i].morphLength;
				}
			}
			
			float elementLength = GetElementLength();
			if( audioClip != null ) {
				playingData.playingLength = audioClip.length;
				if( playingData.playingLength <= morphTotalLength ) {
					playingData.playingLength = morphTotalLength;
					elementLength = 0.0f;
				} else {
					if( morphZeroCount > 0 ) {
						elementLength = (playingData.playingLength - morphTotalLength) / (float)morphZeroCount;
					}
				}
			} else {
				playingData.playingLength = morphTotalLength + elementLength * (float)morphZeroCount;
			}
			if( elementLength > 0.0f ) {
				// Allocate each morphLength.
				for( int i = 0; i < playingData.morphDataList.Count; ++i ) {
					char ch = playingData.morphDataList[i].morphChar;
					if( playingData.morphDataList[i].morphLength == 0.0f ) {
						if( ch != '^' && ch != '`' ) {
							MorphData morphData = playingData.morphDataList[i];
							morphData.morphLength = elementLength;
							playingData.morphDataList[i] = morphData;
						}
					}
				}
				// Fix for each consonantLength.
				float consonantLength = GetConsonantLength();
				for( int i = 0; i < playingData.morphDataList.Count; ++i ) {
					char ch = playingData.morphDataList[i].morphChar;
					if( playingData.morphDataList[i].morphLength == 0.0f ) {
						if( ch != '^' && ch != '`' ) {
							// Nothing.
						} else {
							int beginIndex = i;
							for( ++i; i < playingData.morphDataList.Count; ++i ) {
								ch = playingData.morphDataList[i].morphChar;
								if( ch != '^' && ch != '`' ) {
									float morphLength = playingData.morphDataList[i].morphLength;
									float tempLength = 0.0f;
									if( consonantLength * 2.0f <= morphLength ) {
										MorphData morphData = playingData.morphDataList[i];
										morphData.morphLength = morphLength - consonantLength;
										playingData.morphDataList[i] = morphData;
										tempLength = consonantLength;
									} else {
										MorphData morphData = playingData.morphDataList[i];
										morphData.morphLength = morphLength * 0.5f;
										playingData.morphDataList[i] = morphData;
										tempLength = morphData.morphLength;
									}
									{
										MorphData morphData = playingData.morphDataList[beginIndex];
										morphData.morphLength = tempLength;
										playingData.morphDataList[beginIndex] = morphData;
									}
									break;
								} else {
									if( playingData.morphDataList[i].morphLength == 0.0f ) {
										// Nothing.
									} else {
										break; // Skip processing.( Exception case, no effects. )
									}
								}
							}
						}
					}
				}
			}
		} else {
			if( audioClip != null ) {
				playingData.playingLength = audioClip.length;
			}
		}
		
#if false
		if( playingData.morphDataList != null ) {
			for( int i = 0; i < playingData.morphDataList.Count; ++i ) {
				Debug.Log( "" + playingData.morphDataList[i].morphName +
							":" + playingData.morphDataList[i].morphChar +
							":" + playingData.morphDataList[i].morphLength );
			}
		}
#endif
		
		return playingData;
	}
	
	List<MorphData> _ParseMorphText( string morphText )
	{
		if( string.IsNullOrEmpty( morphText ) ) {
			return null;
		}
		
		//Debug.Log( "_ParseMorphText:" + morphText ); // for Debug.
		
		System.Text.StringBuilder alphabets = new System.Text.StringBuilder();
	
		List<MorphData> morphDataList = new List<MorphData>();

		bool isBeforeAlphabet = false;
		bool isBeforeMorph = false;
		
		int textPos = 0;
		for(;;) {
			if( textPos >= morphText.Length ) {
				break;
			}
			
			string japanesePronun = null;
			char ch = morphText[textPos];
			
			if( ch == '[' ) {
				int beginPos = (++textPos);
				for( ; textPos < morphText.Length; ++textPos ) {
					if( morphText[textPos] == ']' ) {
						int milliSeconds = MMD4MecanimCommon.ToInt( morphText, beginPos, textPos - beginPos );
						if( isBeforeMorph && morphDataList.Count > 0 ) {
							MorphData morphData = morphDataList[morphDataList.Count - 1];
							morphData.morphLength = (float)milliSeconds * 0.001f;
							morphDataList[morphDataList.Count - 1] = morphData;
						} else {
							MorphData morphData = new MorphData();
							morphData.morphChar = ' ';
							morphData.morphName = "";
							morphData.morphLength = (float)milliSeconds * 0.001f;
							morphDataList.Add( morphData );
						}
						++textPos;
						break;
					}
				}
				isBeforeMorph = false;
			} else if( _IsEscape( ch ) ) {
				// ^ ... alveolar
				// ` ... bilabial
				// / ... staccato
				// - ... -
				// ~ ... -
				MorphData morphData = new MorphData();
				morphData.morphChar = ch;
				morphData.morphName = "";
				switch( ch ) {
				case '^':
					morphData.morphName = hiraMorphNameList[2]; // u
					break;
				case '`':
					morphData.morphName = ""; // n
					break;
				case '/':
				case '-':
				case '~':
					if( morphDataList.Count > 0 ) {
						morphData.morphName = morphDataList[morphDataList.Count - 1].morphName;
					}
					break;
				}
				isBeforeMorph = true;
				morphDataList.Add( morphData );
				++textPos;
			} else if( MMD4MecanimCommon.IsAlphabet( ch ) ) {
				// for English.
				string tempString = null;
				bool processedAnything = false;
				ch = MMD4MecanimCommon.ToHalfLower( ch );
				if( textPos + 1 < morphText.Length && MMD4MecanimCommon.IsAlphabet( morphText[textPos + 1] ) ) {
					char ch2 = MMD4MecanimCommon.ToHalfLower( morphText[textPos + 1] );
					if( textPos + 2 < morphText.Length && MMD4MecanimCommon.IsAlphabet( morphText[textPos + 2] ) ) {
						char ch3 = MMD4MecanimCommon.ToHalfLower( morphText[textPos + 2] );
						alphabets.Remove( 0, alphabets.Length );
						alphabets.Append( ch );
						alphabets.Append( ch2 );
						alphabets.Append( ch3 );
						if( englishPhraseDictionary.TryGetValue( alphabets.ToString(), out tempString ) ) {
							_AddMorphData( morphDataList, tempString );
							processedAnything = true;
							textPos += 3;
						}
					}
					if( !processedAnything ) {
						alphabets.Remove( 0, alphabets.Length );
						alphabets.Append( ch );
						alphabets.Append( ch2 );
						if( englishPhraseDictionary.TryGetValue( alphabets.ToString(), out tempString ) ) {
							_AddMorphData( morphDataList, tempString );
							processedAnything = true;
							textPos += 2;
						}
					}
				}
				if( !processedAnything ) {
					if( !isBeforeAlphabet && ( morphText[textPos] == 'I' || morphText[textPos] == '\uFF29' ) ) {
						_AddMorphData( morphDataList, "ai" );
					} else {
						if( textPos > 0 && MMD4MecanimCommon.ToHalfLower( morphText[textPos - 1] ) == 'e' && ch == 'e' ) { // lee, bee, ...
							_AddMorphData( morphDataList, "-" );
						} else {
							if( englishPronunDictionary.TryGetValue( ch, out tempString ) ) {
								_AddMorphData( morphDataList, tempString );
							} else {
								_AddMorphData( morphDataList, " " );
							}
						}
					}
					processedAnything = true;
					++textPos;
				}
				isBeforeAlphabet = true;
				isBeforeMorph = true;
			} else if( japanesePronunDictionary.TryGetValue( ch, out japanesePronun ) ) {
				string postPhaseText = null;
				if( textPos + 1 < morphText.Length && japanesePostPhraseDictionary.TryGetValue( morphText[textPos + 1], out postPhaseText ) ) {
					string phaseText = null;
					if( japanesePhraseDictionary.TryGetValue( morphText.Substring( textPos, 2 ), out phaseText ) ) {
						_AddMorphData( morphDataList, phaseText );
					} else {
						_AddMorphData( morphDataList, japanesePronun );
						_AddMorphData( morphDataList, postPhaseText );
					}
					textPos += 2;
				} else {
					_AddMorphData( morphDataList, japanesePronun );
					++textPos;
				}
				isBeforeAlphabet = false;
				isBeforeMorph = true;
			} else {
				if( textPos + 1 < morphText.Length ) { // Simulate VOICEROID
					string tempString = null;
					if( punctuatDictionary.TryGetValue( ch, out tempString ) ) {
						_AddMorphData( morphDataList, "   " ); // Simulate VOICEROID
					} else {
						// Nothing
					}
				}
				isBeforeAlphabet = false;
				isBeforeMorph = false;
				++textPos;
			}
		}
		
		return morphDataList;
	}
	
	void _AddMorphData( List<MorphData> morphDataList, string morphScript )
	{
		if( morphDataList != null && morphScript != null ) {
			MorphData morphData = new MorphData();
			for( int i = 0; i < morphScript.Length; ++i ) {
				//Debug.Log( "Char:" + morphScript[i] );
				morphData.morphChar = morphScript[i];
				morphData.morphName = "";
				switch( morphData.morphChar ) {
				case 'a': morphData.morphName = hiraMorphNameList[0]; break;
				case 'i': morphData.morphName = hiraMorphNameList[1]; break;
				case 'u': morphData.morphName = hiraMorphNameList[2]; break;
				case 'e': morphData.morphName = hiraMorphNameList[3]; break;
				case 'o': morphData.morphName = hiraMorphNameList[4]; break;
				case 'n': morphData.morphName = hiraMorphNameList[2]; break; // u
				case '^': morphData.morphName = hiraMorphNameList[2]; break; // u
				case '`': morphData.morphName = ""; break; // n
				case '/':
				case '-':
				case '~':
					if( morphDataList.Count > 0 ) {
						morphData.morphName = morphDataList[morphDataList.Count - 1].morphName;
					}
					break;
				}
				morphDataList.Add( morphData );
			}
		}
	}
}

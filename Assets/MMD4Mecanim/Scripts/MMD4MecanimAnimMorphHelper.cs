// - Don't upload model data, motion data, this code in github or public space without permission.
using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

using Morph				= MMD4MecanimModel.Morph;
using MorphCategory		= MMD4MecanimData.MorphCategory;
using MorphType			= MMD4MecanimData.MorphType;
using MorphData			= MMD4MecanimData.MorphData;
using MorphMotionData	= MMD4MecanimData.MorphMotionData;

using AnimData			= MMD4MecanimData.AnimData;
using IAnim				= MMD4MecanimAnim.IAnim;
using IMorph			= MMD4MecanimAnim.IMorph;
using IAnimModel		= MMD4MecanimAnim.IAnimModel;
using MorphMotion		= MMD4MecanimAnim.MorphMotion;

public class MMD4MecanimAnimMorphHelper : MonoBehaviour, IAnimModel
{
	public string							animName = "";
	public string							playingAnimName = "";

	public bool								animEnabled = true;
	public bool								animPauseOnEnd = false;
	public bool								initializeOnAwake = false;
	public bool								animSyncToAudio = true;
	public float							morphSpeed = 0.1f;
	public bool								overrideWeight = false;

	[System.Serializable]
	public class Anim : IAnim
	{
		public string						animName;
		public TextAsset					animFile;
		public AudioClip					audioClip;
		
		[NonSerialized]
		public AnimData						animData;
		[NonSerialized]
		public MorphMotion[]				morphMotionList;

		string IAnim.animatorStateName // Memo: Not supported animatorStateName
		{
			get { return null; }
			set {}
		}
		
		int IAnim.animatorStateNameHash // Memo: Not supported animatorStateNameHash
		{
			get { return 0; }
			set {}
		}
		
		TextAsset IAnim.animFile
		{
			get { return this.animFile; }
			set { this.animFile = value; }
		}
		
		AudioClip IAnim.audioClip
		{
			get { return this.audioClip; }
			set { this.audioClip = value; }
		}
		
		AnimData IAnim.animData
		{
			get { return this.animData; }
			set { this.animData = value; }
		}
		
		MorphMotion[] IAnim.morphMotionList
		{
			get { return this.morphMotionList; }
			set { this.morphMotionList = value; }
		}
	}

	public Anim[]							animList;

	private bool							_initialized;
	private MMD4MecanimModel				_model;
	private AudioSource						_audioSource;
	private Anim							_playingAnim;
	private float							_animTime;
	private float							_morphWeight;
	private float							_weight2;
	private HashSet<MMD4MecanimModel.Morph>	_inactiveModelMorphSet = new HashSet<MMD4MecanimModel.Morph>();

	public virtual bool isProcessing
	{
		get {
			if( _IsPlayingAnim() ) {
				return true;
			}
			if( _inactiveModelMorphSet.Count != 0 ) {
				return true;
			}
			
			return false;
		}
	}
	
	public virtual bool isAnimating
	{
		get {
			if( _IsPlayingAnim() ) {
				return true;
			}
			if( _inactiveModelMorphSet.Count != 0 ) {
				return true;
			}
			
			return false;
		}
	}

	public void PlayAnim( string animName )
	{
		this.animName = animName;
	}

	public void StopAnim()
	{
		this.playingAnimName = "";
	}

	//------------------------------------------------------------------------------------------------------------

	IMorph IAnimModel.GetMorph( string name )
	{
		// Memo: Redirect to MMD4MecanimModel
		if( _model != null ) {
			return _model.GetMorph( name );
		}
		return null;
	}
	
	IMorph IAnimModel.GetMorph( string name, bool startsWith )
	{
		// Memo: Redirect to MMD4MecanimModel
		if( _model != null ) {
			return _model.GetMorph( name, startsWith );
		}
		return null;
	}
	
	int IAnimModel.morphCount
	{
		get
		{
			// Memo: Redirect to MMD4MecanimModel
			if( _model != null && _model.morphList != null ) {
				return _model.morphList.Length;
			}

			return 0;
		}
	}
	
	IMorph IAnimModel.GetMorphAt( int index )
	{
		// Memo: Redirect to MMD4MecanimModel
		if( _model != null && _model.morphList != null ) {
			if( index >= 0 && index < _model.morphList.Length ) {
				return _model.morphList[index];
			}
		}
		
		return null;
	}
	
	int IAnimModel.animCount
	{
		get
		{
			if( this.animList != null ) {
				return this.animList.Length;
			}
			
			return 0;
		}
	}
	
	IAnim IAnimModel.GetAnimAt( int index )
	{
		if( this.animList != null ) {
			if( index >= 0 && index < this.animList.Length ) {
				return this.animList[index];
			}
		}
		
		return null;
	}
	
	Animator IAnimModel.animator // Memo: Not supported animator
	{
		get { return null; }
	}
	
	AudioSource IAnimModel.audioSource
	{
		get
		{
			if( _audioSource == null ) {
				_audioSource = MMD4MecanimCommon.WeakAddComponent< AudioSource >( this.gameObject );
			}
			
			return _audioSource;
		}
	}
	
	bool IAnimModel.animSyncToAudio
	{
		get { return this.animSyncToAudio; }
	}
	
	float IAnimModel.prevDeltaTime // Memo: Require _SyncToAudio & animator
	{
		get { return 0.0f; }
		set {}
	}
	
	IAnim IAnimModel.playingAnim
	{
		get { return _playingAnim; }
		set { _playingAnim = (Anim)value; }
	}
	
	void IAnimModel._SetAnimMorphWeight( IMorph morph, float weight )
	{
		morph.weight = (_morphWeight != 1.0f) ? (weight * _morphWeight) : weight;
	}

	//------------------------------------------------------------------------------------------------------------

	void Awake()
	{
		if( this.initializeOnAwake ) {
			_Initialize();
		}
	}

	void Start()
	{
		_Initialize();
	}

	void Update()
	{
		_UpdateAnim();
		_UpdateMorph();
	}

	void _Initialize()
	{
		if( _initialized ) {
			return;
		}

		_initialized = true;
		_model = this.gameObject.GetComponent< MMD4MecanimModel >();
		if( _model == null ) {
			return;
		}

		_model.Initialize();

		MMD4MecanimAnim.InitializeAnimModel( this );
	}

	void _UpdateAnim()
	{
		if( !this.animEnabled ) {
			_StopAnim();
			return;
		}

		if( _playingAnim != null ) {
			if( string.IsNullOrEmpty(this.playingAnimName) ||
				_playingAnim.animName == null || this.playingAnimName != _playingAnim.animName ) {
				_StopAnim();
			}
		}

		bool updatedAnimModel = false;
		if( _playingAnim == null && !string.IsNullOrEmpty(this.animName) ) {
			if( this.animList != null ) {
				for( int i = 0; i != this.animList.Length; ++i ) {
					if( this.animList[i].animName != null && this.animList[i].animName == this.animName ) {
						_PlayAnim( this.animList[i] );
						updatedAnimModel = true;
						break;
					}
				}
			}
		}

		if( _playingAnim != null ) {
			if( _playingAnim.animData != null ) {
				if( !this.animPauseOnEnd ) {
					if( _animTime >= (float)(_playingAnim.animData.maxFrame - 1) / 30.0f ) { // Bugfix
						_StopAnim();
					}
				}
			} else { // Failsafe.
				_StopAnim();
			}
		}

		if( _playingAnim == null ) {
			return;
		}

		if( !updatedAnimModel ) {
			MMD4MecanimAnim.UpdateAnimModel( this, _playingAnim, _animTime );
		}

		if( _playingAnim != null ) {
			// Postfix for animWeight2
			if( _playingAnim.morphMotionList != null ) {
				for( int i = 0; i != _playingAnim.morphMotionList.Length; ++i ) {
					if( _playingAnim.morphMotionList[i].morph != null ) {
						((Morph)_playingAnim.morphMotionList[i].morph).weight2 = _weight2;
					}
				}
			}

			if( _playingAnim.audioClip != null && _audioSource != null && _audioSource.isPlaying && this.animSyncToAudio ) {
				_animTime = _audioSource.time;
			} else {
				_animTime += Time.deltaTime;
			}

			if( _playingAnim.animData != null ) {
				_animTime = Mathf.Min( _animTime, (float)_playingAnim.animData.maxFrame / 30.0f );
			} else {
				_animTime = 0.0f;
			}
		}
	}

	bool _IsPlayingAnim()
	{
		if( _playingAnim != null && _playingAnim.animData != null ) {
			return _animTime < (float)(_playingAnim.animData.maxFrame - 1) / 30.0f; // Bugfix
		}

		return false;
	}

	void _PlayAnim( Anim anim )
	{
		_StopAnim(); // _playingAnim = null;

		_animTime = 0.0f;
		this.animName = "";
		if( anim != null ) {
			this.playingAnimName = anim.animName;
		}

		MMD4MecanimAnim.UpdateAnimModel( this, anim, _animTime ); // _playingAnim = anim;

		if( _playingAnim != null && _inactiveModelMorphSet != null ) {
			if( _playingAnim.morphMotionList != null ) {
				for( int i = 0; i != _playingAnim.morphMotionList.Length; ++i ) {
					_playingAnim.morphMotionList[i].lastKeyFrameIndex = 0;
					Morph morph = (Morph)(_playingAnim.morphMotionList[i].morph);
					if( morph != null ) {
						_inactiveModelMorphSet.Remove( morph );
					}
				}
			}
		}
	}

	void _StopAnim()
	{
		if( _playingAnim != null && _inactiveModelMorphSet != null ) {
			if( _playingAnim.morphMotionList != null ) {
				for( int i = 0; i != _playingAnim.morphMotionList.Length; ++i ) {
					_playingAnim.morphMotionList[i].lastKeyFrameIndex = 0;
					Morph morph = (Morph)(_playingAnim.morphMotionList[i].morph);
					if( morph != null && (morph.weight != 0.0f || morph.weight2 != 0.0f) ) {
						_inactiveModelMorphSet.Add( morph );
					}
				}
			}
		}
		
		MMD4MecanimAnim.StopAnimModel( this ); // _playingAnim = null;
		_animTime = 0.0f;
		this.playingAnimName = "";
	}
	
	void _UpdateMorph()
	{
		float stepValue = 1.0f;
		if( this.morphSpeed > 0.0f ) {
			stepValue = Time.deltaTime / this.morphSpeed;
		}

		if( _playingAnim != null ) {
			MMD4MecanimCommon.Approx( ref _morphWeight, 1.0f, stepValue );
			MMD4MecanimCommon.Approx( ref _weight2, this.overrideWeight ? 1.0f : 0.0f, stepValue );
		} else {
			MMD4MecanimCommon.Approx( ref _morphWeight, 0.0f, stepValue );
			MMD4MecanimCommon.Approx( ref _weight2, 0.0f, stepValue );
		}
		if( _inactiveModelMorphSet != null ) {
			foreach( var morph in _inactiveModelMorphSet ) {
				MMD4MecanimCommon.Approx( ref morph.weight, 0.0f, stepValue );
				MMD4MecanimCommon.Approx( ref morph.weight2, 0.0f, stepValue );
			}
			_inactiveModelMorphSet.RemoveWhere( s => s.weight == 0.0f && s.weight2 == 0.0f );
		}
	}
}

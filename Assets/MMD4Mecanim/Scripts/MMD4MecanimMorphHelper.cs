// - Don't upload model data, motion data, this code in github or public space without permission.
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MMD4MecanimMorphHelper : MonoBehaviour
{
	public float							morphSpeed = 0.1f;

	public string							morphName;
	public float							morphWeight;
	public bool								overrideWeight;

	protected MMD4MecanimModel				_model;
	private MMD4MecanimModel.Morph			_modelMorph;
	private float							_morphTime;
	private HashSet<MMD4MecanimModel.Morph>	_inactiveModelMorphSet = new HashSet<MMD4MecanimModel.Morph>();
	private float							_weight2 = 0.0f;
	
	public virtual bool isProcessing
	{
		get {
			if( _modelMorph != null ) {
				if( _modelMorph.weight != this.morphWeight ) {
					return true;
				}
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
			if( _modelMorph != null ) {
				if( _modelMorph.weight != this.morphWeight ) {
					return true;
				}
			}
			if( _inactiveModelMorphSet.Count != 0 ) {
				return true;
			}
			
			return false;
		}
	}
	
	protected virtual void Start()
	{
		_model = GetComponent< MMD4MecanimModel >();
		if( _model != null ) {
			_model.Initialize();
		}
	}

	protected virtual void Update()
	{
		_UpdateMorph( Time.deltaTime );
	}
	
	public virtual void ForceUpdate()
	{
		_UpdateMorph( 0.0f );
	}

	void _UpdateMorph( float deltaTime )
	{
		_UpdateModelMorph();
		
		float stepValue = 1.0f;
		if( this.morphSpeed > 0.0f ) {
			stepValue = deltaTime / this.morphSpeed;
		}
		
		if( _modelMorph != null ) {
			MMD4MecanimCommon.Approx( ref _modelMorph.weight, this.morphWeight, stepValue );
			MMD4MecanimCommon.Approx( ref _weight2, this.overrideWeight ? 1.0f : 0.0f, stepValue );
			_modelMorph.weight2 = _weight2;
		} else {
			MMD4MecanimCommon.Approx( ref _weight2, 1.0f, stepValue );
		}
		
		if( _inactiveModelMorphSet != null ) {
			foreach( var morph in _inactiveModelMorphSet ) {
				MMD4MecanimCommon.Approx( ref morph.weight, 0.0f, stepValue );
				MMD4MecanimCommon.Approx( ref morph.weight2, 0.0f, stepValue );
			}
			_inactiveModelMorphSet.RemoveWhere( s => s.weight == 0.0f && s.weight2 == 0.0f );
		}
	}
	
	void _UpdateModelMorph()
	{
		if( _modelMorph != null ) {
			if( string.IsNullOrEmpty( this.morphName ) || _modelMorph.name != this.morphName ) {
				if( _modelMorph.weight != 0.0f || _modelMorph.weight2 != 0.0f ) {
					_inactiveModelMorphSet.Add( _modelMorph );
				}
				_modelMorph = null;
			}
		}
		
		if( _modelMorph == null ) {
			if( _model != null ) {
				_modelMorph = _model.GetMorph( this.morphName );
				if( _modelMorph != null && _inactiveModelMorphSet != null ) {
					_inactiveModelMorphSet.Remove(_modelMorph);
				}
			}
		}
	}
}

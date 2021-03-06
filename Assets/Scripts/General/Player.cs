﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

public class Player : Controller
{
	/* Instance Vars */

	[SerializeField]
	private KeyCode up = KeyCode.W;
	[SerializeField]
	private KeyCode left = KeyCode.A;
	[SerializeField]
	private KeyCode down = KeyCode.S;
	[SerializeField]
	private KeyCode right = KeyCode.D;
	[SerializeField]
	private KeyCode use_ability = KeyCode.Space;
	[SerializeField]
	private KeyCode grapple = KeyCode.LeftShift;
	[SerializeField]
	private KeyCode interact = KeyCode.E;

	private Vector2 direction;


	[SerializeField]
	public Stat movespeed = new Stat(0, 0);

	public bool canMove = true;

	// Clone variables
	[SerializeField]
	private float cloneTimerMax;
	private float cloneTimerFixed;
	private float cloneTimerNormal;

	private Timeline<KeyCode> updateInputs;
	private Timeline<KeyCode> fixedInputs;

	private BehaviorState normal;
	private BehaviorState passive;
	private BehaviorState recording;
	private BehaviorState playing;

	private GameObject clone;
	private GameObject selfPref;

	private Vector3 replayStartPos;
	private Quaternion replayStartRot;

	[SerializeField]
	private GrappleHook hook;

	//DEBUG demo update rate difference
	private int updates;
	private int fixedUpdates;

	/* Instance Methods */
	public override void Awake ()
	{
		base.Awake ();

		canMove = true;

		normal = new BehaviorState("normal", this.normal_update, this.normal_fupdate, this.lupdate);
		passive = new BehaviorState ("passive", this.passive_update, this.passive_fupdate, this.lupdate);
		recording = new BehaviorState ("recording", this.recording_update, this.recording_fupdate, this.lupdate);
		playing = new BehaviorState ("playing", this.playing_update, this.playing_fupdate, this.lupdate);

		setState (normal);

		direction = Vector2.zero;

		updateInputs = new Timeline<KeyCode>();
		fixedInputs = new Timeline<KeyCode> ();

		fixedInputs.setLooping (true);
		updateInputs.setLooping (true);

		clone = null;
		selfPref = Resources.Load<GameObject> ("Player");

		replayStartPos = Vector3.zero;
		replayStartRot = Quaternion.identity;
	}

	public void Start()
	{
		
	}

	// --- Normal ---
	private void normal_update()
	{
//		Debug.Log ("NU: " + (++updates).ToString().PadLeft(7, '0')); //DEBUG demo update rate diff
		if (Input.GetKeyDown (use_ability))
		{
			if (clone != null)
				Destroy (clone);
			else
			{
				clone = Instantiate<GameObject> (selfPref, transform.position, transform.rotation);
				Physics2D.IgnoreCollision (GetComponent<Collider2D> (), clone.GetComponent<Collider2D> ());

				//change the color of the clone
				SpriteRenderer cloneSR = clone.GetComponent<SpriteRenderer> ();
				Color cloneCol = cloneSR.color;
				cloneSR.color = new Color (cloneCol.r, cloneCol.g, cloneCol.b, 0.5f);

				//set the states of the clone and the player
				Player other = clone.GetComponent<Player> ();
				other.setState (other.recording);
				other.cloneTimerFixed = 0f;
				other.cloneTimerNormal = 0f;
				other.clone = gameObject;
				other.fixedInputs.addLoopListener (other.resetPosition);

				setState (passive);

				CameraManager.scene_cam.setTarget (clone.transform);
			}
		}

		if (Input.GetKeyDown (grapple))
			hook.Launch ();

		if (Input.GetKeyDown (interact))
			doInteract ();
	}

	private void normal_fupdate()
	{
		float horizontal = Input.GetKey (left) ? -1f : Input.GetKey (right) ? 1f : 0f;
		float vertical = Input.GetKey (down) ? -1f : Input.GetKey (up) ? 1f : 0f;

		move (horizontal, vertical);

//		Debug.Log ("FU: " + (++fixedUpdates).ToString().PadLeft(7, '0')); //DEBUG demo update rate diff
	}

	// --- Passive ---
	private void passive_update()
	{
		
	}

	private void passive_fupdate()
	{

	}

	// --- Recording ---
	private void recording_update()
	{
		//cloneTimer -= Time.deltaTime;
		cloneTimerNormal += Time.deltaTime;
		if (cloneTimerNormal >= cloneTimerMax)
		{
			//record the starting position info for the replay loop
			replayStartPos = transform.position = clone.transform.position;
			replayStartRot = transform.rotation = clone.transform.rotation;

			//ensure that the two timelines conclude at the same time
			updateInputs.addEvent (cloneTimerMax, KeyCode.None);
			fixedInputs.addEvent (cloneTimerMax, KeyCode.None);

			//return control to the original player
			Player other = clone.GetComponent<Player> ();
			other.setState (other.normal);
			CameraManager.scene_cam.setTarget (clone.transform);

			//begin replay
			setState (playing);
		}

		normal.update ();
		KeyCode[] keys = readKeyPresses (true);

		if (keys.Length > 0f) 
		{
			updateInputs.addEvent (cloneTimerNormal, keys);
		}
	}

	private void recording_fupdate()
	{
		normal.fixedUpdate ();
		cloneTimerFixed += Time.fixedDeltaTime;
		KeyCode[] keys = readKeyPresses ();

		if (keys.Length > 0f) 
		{
			fixedInputs.addEvent (cloneTimerFixed, keys);
		}
	}

	// --- Playing ---
	private void playing_update()
	{
		KeyCode[] keys;
		updateInputs.simulate(Time.deltaTime, out keys);

		if (keyRecorded (grapple, keys))
			hook.Launch ();

		if (keyRecorded (interact, keys))
			doInteract ();
	}

	private void playing_fupdate()
	{
		KeyCode[] dump_keys;
		fixedInputs.simulate (Time.fixedDeltaTime, out dump_keys);

		float horizontal = keyRecorded (left,dump_keys) ? -1f : keyRecorded (right, dump_keys) ? 1f : 0f;
		float vertical = keyRecorded (down, dump_keys) ? -1f : keyRecorded (up, dump_keys) ? 1f : 0f;
		move (horizontal, vertical);
	}

	private void lupdate()
	{
//		Debug.Log ("-------------------"); //DEBUG demo update rate diff
	}

	// --- Utilities ---

	// Check if a key was pressed in the current frame of the recording
	private bool keyRecorded(KeyCode key, KeyCode[] tape)
	{
		foreach (KeyCode k in tape)
			if (k == key)
				return true;
		return false;
	}

	// Read the keys currently pressed and add them to an array for recording
	private KeyCode[] readKeyPresses(bool keyDown = false)
	{
		List<KeyCode> keys = new List<KeyCode> ();
		foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
		{
			if (Input.GetKey (key) && !keyDown)
			{
				keys.Add (key);
			}
			else if (Input.GetKeyDown (key))
			{
				keys.Add (key);
			}
		}

		return keys.ToArray ();
	}

	// Take a dX and dY and translate it into rigidbody movement
	private void move(float horizontal, float vertical)
	{
		Vector2 movementVector = new Vector2 (horizontal, vertical);

		if(canMove)
			physbody.AddForce (movementVector * movespeed.value);
		else
			physbody.velocity = Vector2.zero;

		if (movementVector != Vector2.zero)
			direction = movementVector;
		facePoint (direction + (Vector2)transform.position);
	}

	private void resetPosition()
	{
		if(clone != null)
		{
			transform.position = replayStartPos;
			transform.rotation = replayStartRot;
		}
	}

	private void doInteract()
	{
		Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 0.5f); 

		foreach (Collider2D col in hits)
		{
			SimpleSwitch swt = col.gameObject.GetComponent<SimpleSwitch> ();
			if (swt != null)
			{
				swt.ToggleSwitch ();
				Debug.Log (name + " interacted.");
			}
		}
	}

	public void OnDestroy()
	{
		DestroyImmediate (hook.gameObject);
	}
}

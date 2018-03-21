﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;


public class PlayerController : EntityBase {	
	private static float BasicSize = 0.5f;

	public enum State {
		Normal,
		Jump,
	};


	public State state = State.Normal;
	public Animator anim;
	
	public int jumpGape;
	public int PlayerIndex = 0;
	public Vector2 velocityOut;
	public Vector2 velocitA;
	public Vector2 deVelocity;

	public SpriteRenderer sprite;
	public Dictionary<Collider2D, int> touching = new Dictionary<Collider2D, int>();
	public Transform eating = null;

	public AudioSource bornAudio;
	public AudioSource jumpAudio;
	public AudioSource eatAudio;

	public float size = 0;
	

	protected override void FStart() {
		rb = GetComponent<Rigidbody2D>();
		bc = GetComponent<BoxCollider2D>();

		if (Network.isServer) {
			rb.simulated = true;
			SetSize();
		}
	}
	
	void Update () {
		if ((Network.isClient || Network.isServer)) {
			
			float horizonDirection = 0;
			bool downCommand = false;
			bool jumpCommand = false;
			bool eatCommand = false;


			if (PlayerIndex == 0) {
				if (isDead) {
					if (Input.GetKeyDown(KeyCode.E)) {
						GameEngine.direct.OnReborn(this);
					}
					return;
				}
				horizonDirection = (Input.GetAxisRaw("LeftHorizon") > 0 ? 1 : 0) + (Input.GetAxisRaw("LeftHorizon") < 0 ? -1 : 0);
				downCommand = Input.GetAxisRaw("LeftVertical") < 0;
				jumpCommand = Input.GetKeyDown(KeyCode.Space);
				eatCommand = Input.GetKeyDown(KeyCode.E);

			} else if (PlayerIndex == 1) {
				if (isDead) {
					if (Input.GetKeyDown(KeyCode.Period)) {
						GameEngine.direct.OnReborn(this);
					}
					return;
				}
				horizonDirection = (Input.GetAxisRaw("RightHorizon") > 0 ? 1 : 0) + (Input.GetAxisRaw("RightHorizon") < 0 ? -1 : 0);
				downCommand = Input.GetAxisRaw("RightVertical") < 0;
				jumpCommand = Input.GetKeyDown(KeyCode.Comma);
				eatCommand = Input.GetKeyDown(KeyCode.Period);

			} else if (PlayerIndex == 2) {
				if (isDead) {
					if (Input.GetAxisRaw("LHPanel") > 0) {
						GameEngine.direct.OnReborn(this);
					}
					return;
				}
				horizonDirection = (Input.GetAxisRaw("PS4LeftHorizon") > 0 ? 1 : 0) + (Input.GetAxisRaw("PS4LeftHorizon") < 0 ? -1 : 0);
				downCommand = Input.GetAxisRaw("PS4LeftVertical") > 0;
				jumpCommand = Input.GetAxisRaw("LVPanel") < 0;
				eatCommand = Input.GetAxisRaw("LHPanel") > 0;

			} else if (PlayerIndex == 3) {
				if (isDead) {
					if (Input.GetKeyDown(KeyCode.Mouse1)) {
						GameEngine.direct.OnReborn(this);
					}
					return;
				}
				horizonDirection = (Input.GetAxisRaw("PS4RightHorizon") > 0 ? 1 : 0) + (Input.GetAxisRaw("PS4RightHorizon") < 0 ? -1 : 0);
				downCommand = Input.GetAxisRaw("PS4RightVertical") > 0;
				jumpCommand = Input.GetAxisRaw("PS4RightVerticalPanel") > 0;
				eatCommand = Input.GetAxisRaw("PS4RightHorizonPanel") > 0;
			}

			if (eatCommand) {
				CmdDigestive();

			} else if (downCommand) {
				CmdCrouch(horizonDirection);

			} else if (jumpCommand) {
				CmdJump(jumpCommand);

			} else if (horizonDirection != 0) {
				CmdMove(horizonDirection);

			} else {
				CmdIdle();
			}
			
			if (eating) {
				eating.transform.position = Vector2.Lerp(eating.transform.position, transform.position, 0.1f);
			}
		}

		if (Network.isServer) {
			if (touching.Count == 0) {
				state = State.Jump;
				RpcState("Jump");
			}

			if (isInvincible) {
				invincibleTimer += Time.deltaTime;
				
				if (invincibleTimer < 2f) {
					float remainder = invincibleTimer % 0.2f;
					sprite.color = remainder > 0.1f ? Color.white : new Color(1 , 1 , 1 , 0.4f);

				} else {
					invincibleTimer = 0;
					sprite.color = Color.white;
					isInvincible = false;
				}
			}

			RpcApplyTransform(transform.position , transform.localScale);
		}		
	}

	protected override void FFixedUpdate() {
		if (!isDead) {
			if (velocityOut.x != 0) {
				velocitA.x = velocitA.x + velocityOut.x;
				velocityOut = Vector2.zero;
			}

			if (touching.ContainsValue(2) && velocitA.x > 1) {
				velocitA.x = 0;

			} else if(touching.ContainsValue(3) && velocitA.x < 1) {
				velocitA.x = 0;
			}

			velocitA.y = rb.velocity.y - GameEngine.direct.jumpYDec * Time.deltaTime ;
			rb.velocity = velocitA;
		}
	}

	[ClientRpc]
	public void RpcApplyTransform(Vector2 position, Vector2 localScale) {
		transform.position = position;
	}
		
	[ClientRpc]
	public void RpcState(string state) {
		anim.Play(state);
	}

	[Command]
	public void CmdRegist(int PlayerIndex, int hp, int jumpGape) {
		this.PlayerIndex = PlayerIndex;
		this.hp = hp;
		this.jumpGape = jumpGape;
		GameEngine.direct.OnRegist(this);
		SetSize();
	}

	[Command]
	public void CmdCrouch(float direction) {
		if (anim.GetCurrentAnimatorStateInfo(0).IsTag("Eat")) {
			return;
		}

		if (state != State.Jump) {//地面發呆
			Facing(direction);

			if (!anim.GetCurrentAnimatorStateInfo(0).IsTag("Crouch") && !anim.GetCurrentAnimatorStateInfo(0).IsTag("Jump")) {
				RpcState("Crouch");
			}

			if (rb.velocity.x != 0) {
				if (!IsSlideing()) {
					velocitA.x = Decelerator(velocitA.x, GameEngine.direct.walkXDec, 0);
				} else {
					velocitA.x = Decelerator(velocitA.x, GameEngine.direct.iceXDec, 0);
				}
			}
		} else {//空中移動
			velocitA.x = Decelerator(velocitA.x, GameEngine.direct.iceXDec, 0);
		}
	}

	[Command]
	public void CmdDigestive() {
		if (anim.GetCurrentAnimatorStateInfo(0).IsTag("Eat")) {
			return;
		}

		if (state != State.Jump) {
			bool eatCheck = false;
			foreach (Transform unit in GameEngine.direct.units) {
				if (Vector2.Distance(transform.position, unit.position) <= size * 0.25f + 3) {
					eatCheck = true;
					break;
				}
			}

			if (eatCheck) {
				RpcState("Digestive");
			} else {
				RpcState("EatHorizon");
			}
			Eat();
		}
	}

	[Command]
	public void CmdEat() {
		if (anim.GetCurrentAnimatorStateInfo(0).IsTag("Eat")) {
			return;
		}

		if (state != State.Jump) {
			RpcState("EatHorizon");
			Eat();
		} 
	}

	[Command]
	public void CmdJump(bool jumpCommand) {
		if (anim.GetCurrentAnimatorStateInfo(0).IsTag("Eat")) {
			return;
		}

		if (jumpCommand && state != State.Jump) {
			jumpAudio.Play();
			rb.AddForce(Vector2.up * GameEngine.direct.jumpYForce * ((jumpGape - size) / jumpGape), ForceMode2D.Impulse);
			state = State.Jump;
			RpcState("Jump");
			return;
		}
	}

	protected void Facing(float direction) {
		if (direction != 0) {
			transform.localScale = new Vector3(direction * Mathf.Abs(transform.localScale.x), transform.localScale.y, 1);
		}
	}

	[Command]
	public void CmdMove(float direction) {
		if (anim.GetCurrentAnimatorStateInfo(0).IsTag("Eat")) {
			return;
		}
		
		if ((direction == 1 && touching.ContainsValue(2)) || (direction == -1 && touching.ContainsValue(3))) {
			direction = 0;
		}

		if (state != State.Jump) {//地面發呆
			if (direction != 0) {				
				if (!anim.GetCurrentAnimatorStateInfo(0).IsTag("Walk")) {
					RpcState("Walk");
				}

				Facing(direction);

				if (!IsSlideing()) {
					velocitA.x = Accelerator(velocitA.x, direction * GameEngine.direct.walkXAcc, direction * GameEngine.direct.walkXSpeed);
				} else {
					velocitA.x = Accelerator(velocitA.x, direction * GameEngine.direct.iceXAcc, direction * GameEngine.direct.walkXSpeed);
				}				
				return;
			}
			CmdIdle();
		} else  {//空中移動
			if (direction != 0) {
				Facing(direction);
				velocitA.x = Accelerator(velocitA.x, direction * GameEngine.direct.jumpXAcc, direction * GameEngine.direct.jumpXSpeed);
			}				
		}
	}

	[Command]
	public void CmdIdle() {
		if (anim.GetCurrentAnimatorStateInfo(0).IsTag("Eat")) {
			if (state != State.Jump) {
				if (rb.velocity.x != 0) {
					if (!IsSlideing()) {
						velocitA.x = Decelerator(velocitA.x, GameEngine.direct.walkXDec, 0);
					} else {
						velocitA.x = Decelerator(velocitA.x, GameEngine.direct.iceXDec, 0);
					}
				}
			} else {//空中移動
				velocitA.x = Decelerator(velocitA.x, GameEngine.direct.iceXDec, 0);
			}
			return;
		}

		if (state != State.Jump) {//地面發呆
			if (!anim.GetCurrentAnimatorStateInfo(0).IsTag("Idle")) {
				RpcState("Idle");
			}

			if (rb.velocity.x != 0) {
				if (!IsSlideing()) {
					velocitA.x = Decelerator(velocitA.x, GameEngine.direct.walkXDec, 0);
				} else {
					velocitA.x = Decelerator(velocitA.x, GameEngine.direct.iceXDec, 0);
				}
			}
		} else {//空中移動
			velocitA.x = Decelerator(velocitA.x, GameEngine.direct.iceXDec, 0);
		}
	}

	public override void Attack(int damage, bool firstOrder = false) {
		if (!isInvincible || firstOrder) {
			isInvincible = true;
			hp = hp - damage;
			if (hp == 0) {
				OnDead();
			} else {
				SetSize();
			}
		}
	}

	public override void OnDead() {
		isDead = true;
		GameEngine.direct.OnDead(this);
		rb.simulated = false;
		transform.localScale = Vector3.zero;
		velocitA = Vector2.zero;
	}

	public void Reborn() {
		bornAudio.Play();
		rb.simulated = true;
		SetSize();
		isDead = false;
		state = State.Jump;
		touching = new Dictionary<Collider2D, int>();
	}
	
	protected void OnCollisionExit2D(Collision2D collision) {
		if (Network.isServer) {
			touching.Remove(collision.collider);

			if (isDead) {
				return;
			}
		}
	}

	protected override void FOnCollisionEnter2D(Collision2D collision) {
		if (isDead) {
			return;
		}

		if (collision.transform.tag == "End") {
			GameEngine.direct.OnVictory();

		} else if (collision.transform.tag == "Dead" || collision.transform.tag == "Scene") {
			OnDead();

		} else if (collision.transform.tag == "Slime") {
			PlayerController ipc = collision.gameObject.GetComponent<PlayerController>();

			float xv = velocitA.x >= 0 ? 1 : -1;
			float ixv = ipc.velocitA.x >= 0 ? 1 : -1;

			float xe = velocitA.x + xv * size;
			float ixe = ipc.velocitA.x + ixv * ipc.size;

			if (Mathf.Abs(xe) > Mathf.Abs(ixe) && velocitA.x != 0) {
				Debug.Log(name + "[1]:" + name + "/" + xe + "撞" + collision.gameObject.name + "/" + ixe);

				if (Mathf.Abs(xe + ixe) >= 6) {
					collision.gameObject.GetComponent<PlayerController>().velocityOut.x = xv * Mathf.Abs(xe + ixe) * 0.5f;
				}

				/*
				if (xv == ixv) {
					Debug.Log(name + "[1]:" + name + "/" + xe + "撞" + collision.gameObject.name + "/" + ixe);

					if (Mathf.Abs(xe + ixe) >= 6) {
						collision.gameObject.GetComponent<PlayerController>().velocityOut.x = xv * Mathf.Abs(xe + ixe) * 0.5f;
					}
				} else {
					Debug.Log(name + "[2]:" + name + "/" + xe + "撞" + collision.gameObject.name + "/" + ixe);

					if (Mathf.Abs(xe + ixe) >= 6) {
						collision.gameObject.GetComponent<PlayerController>().velocityOut.x = xv * Mathf.Abs(xe + ixe) * 0.5f;
					}
				}*/
			}
		}
	}

	protected override void FOnCollisionStay2D(Collision2D collision) {
		if (isDead ) {
			return;
		}

		if (collision.contacts.Length == 0) {
			return;
		}

		Vector2 pointOfContact = collision.contacts[0].normal;

		//Left
		if (pointOfContact == new Vector2(-1, 0)) {
			TouchSide(collision, 2);

			//Right	
		} else if (pointOfContact == new Vector2(1, 0)) {
			TouchSide(collision, 3);

			//Bottom
		} else if (pointOfContact == new Vector2(0, -1)) {
			TouchSide(collision, 1);

			//Top
		} else if (pointOfContact == new Vector2(0, 1)) {
			TouchSide(collision, 0);
		}
	}

	private void TouchSide(Collision2D collision, int side) {
		if (side == 0 && !touching.ContainsValue(0)) {
			RpcState("Idle");
			//rb.velocity = Vector2.zero;
			state = State.Normal;
		}

		if (!touching.ContainsKey(collision.collider)) {
			touching.Add(collision.collider, side);
		} else {
			touching[collision.collider] = side;
		}
	}

	protected void Eat() {
		eatAudio.Play();
		foreach (Transform unit in GameEngine.direct.units) {
			if (Vector2.Distance(transform.position, unit.position) <= (BasicSize + size * 0.125f) + 2) {
				eating = unit;
				unit.GetComponent<EntityBase>().OnDead();
				hp++;
				SetSize();
				return;
			}
		}
	}
	
	protected void SetSize() {
		size = hp;
		float tempsize = (BasicSize + size * 0.125f) * (transform.localScale.x != 0 ?(transform.localScale.x / Mathf.Abs(transform.localScale.x)) : 1);
		transform.localScale = new Vector3(tempsize, Mathf.Abs(tempsize), 1);
		GameEngine.direct.ResetCamera();		
	}

	public bool IsSlideing() {
		foreach (Collider2D collider in touching.Keys) {
			if (collider.name == "Ice") {
				return true;
			}
		}
		return false;
	}

	public float Accelerator(float value , float acc , float maxValue) {
		acc = acc * Time.deltaTime;

		if (maxValue > 0) {
			return value + acc >= maxValue ? maxValue : value + acc;

		} else if (maxValue < 0) {
			return value + acc <= maxValue ? maxValue : value + acc;
		}
		return 0;
	}

	public float Decelerator(float value, float dec, float finalValue) {
		dec = dec * Time.deltaTime;

		if (value > finalValue) {
			return value - dec <= finalValue ? finalValue : value - dec;

		} else if (value < finalValue) {
			return value + dec >= finalValue ? finalValue : value + dec;
		}
		return finalValue;
	}

	private void OnTriggerEnter2D(Collider2D collider) {
		if (Network.isServer) {
			GameEngine.RegistCheckPoint(collider.gameObject.name);
		}
	}
}
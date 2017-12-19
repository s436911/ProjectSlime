﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class EntityBase : NetworkBehaviour {
	public bool isInvincible = false;
	public bool eatAble = true;
	public float invincibleTimer;
	public int attack = 1;
	public int hp = 2;

	protected Rigidbody2D rb;
	protected BoxCollider2D bc;

	void Start() {
		FStart();
	}

	protected virtual void FStart() {
		rb = GetComponent<Rigidbody2D>();
		bc = GetComponent<BoxCollider2D>();

		if (isServer) {
			rb.simulated = true;
		}
	}

	private void OnCollisionStay2D(Collision2D collision) {
		if (isServer) {
			FOnCollisionStay2D(collision);
		}
	}

	protected virtual void FOnCollisionStay2D(Collision2D collision) {
		EntityBase other = collision.gameObject.GetComponent<EntityBase>();
		if (other && attack > 0) {
			other.Attack(attack);
		}
	}

	public virtual void Attack(int damage) {
		if (!isInvincible) {
			hp--;
			isInvincible = true;
			if (hp == 0) {
				Destroy(gameObject);
			}
		}
	}
}
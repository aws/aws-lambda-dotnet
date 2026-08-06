#!/usr/bin/env python3
"""Inject a pre-existing Lambda execution role into a conformance SAM template.

Rewrites the given template in place so that every AWS::Serverless::Function
uses the provided execution role ARN, and removes the self-created
DurableFunctionRole resource. Used by CI to avoid creating an IAM role per
deploy; the checked-in template stays self-contained for local runs.

CloudFormation short-form intrinsic tags (!Sub, !GetAtt, !Ref, ...) are
preserved through a tag-aware load/dump round-trip.

Usage:
    python3 scripts/inject_execution_role.py --template template_step.yaml \
        --role-arn arn:aws:iam::123456789012:role/my-execution-role

Requires PyYAML (already a dependency of the conformance runner).
"""

from __future__ import annotations

import argparse
import sys

import yaml

SELF_CREATED_ROLE = "DurableFunctionRole"
FUNCTION_TYPE = "AWS::Serverless::Function"


class CfnTag:
    """Opaque holder for a CloudFormation short-form tag (e.g. !Sub, !GetAtt)."""

    def __init__(self, tag: str, value: object) -> None:
        self.tag = tag
        self.value = value


class CfnLoader(yaml.SafeLoader):
    """SafeLoader that wraps unknown (CloudFormation) tags instead of failing."""


def _construct_cfn_tag(loader: CfnLoader, suffix: str, node: yaml.Node) -> CfnTag:
    if isinstance(node, yaml.ScalarNode):
        value: object = loader.construct_scalar(node)
    elif isinstance(node, yaml.SequenceNode):
        value = loader.construct_sequence(node, deep=True)
    else:
        value = loader.construct_mapping(node, deep=True)
    return CfnTag(node.tag, value)


CfnLoader.add_multi_constructor("!", _construct_cfn_tag)


class CfnDumper(yaml.SafeDumper):
    """SafeDumper that re-emits wrapped CloudFormation tags verbatim."""


def _represent_cfn_tag(dumper: CfnDumper, data: CfnTag) -> yaml.Node:
    if isinstance(data.value, str):
        return dumper.represent_scalar(data.tag, data.value)
    if isinstance(data.value, list):
        return dumper.represent_sequence(data.tag, data.value)
    return dumper.represent_mapping(data.tag, data.value)


CfnDumper.add_representer(CfnTag, _represent_cfn_tag)


def _safe_load_cfn(stream: object) -> object:
    """Safely load a CloudFormation template.

    Equivalent to yaml.safe_load (CfnLoader extends yaml.SafeLoader) while
    additionally preserving CloudFormation short-form tags.
    """
    loader = CfnLoader(stream)
    try:
        return loader.get_single_data()
    finally:
        loader.dispose()


def inject(template_path: str, role_arn: str) -> int:
    """Rewrite template_path in place; return the number of functions updated."""
    with open(template_path, encoding="utf-8") as f:
        doc = _safe_load_cfn(f)

    resources = doc.get("Resources")
    if not isinstance(resources, dict):
        raise SystemExit(f"{template_path}: no Resources section found")

    resources.pop(SELF_CREATED_ROLE, None)

    updated = 0
    for resource in resources.values():
        if resource.get("Type") == FUNCTION_TYPE:
            resource.setdefault("Properties", {})["Role"] = role_arn
            updated += 1

    if updated == 0:
        raise SystemExit(f"{template_path}: no {FUNCTION_TYPE} resources found")

    with open(template_path, "w", encoding="utf-8") as f:
        yaml.dump(doc, f, Dumper=CfnDumper, sort_keys=False)

    return updated


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument(
        "--template",
        required=True,
        help="Path to the SAM template to rewrite in place.",
    )
    parser.add_argument(
        "--role-arn",
        required=True,
        help="Execution role ARN to set on every serverless function.",
    )
    args = parser.parse_args()

    updated = inject(args.template, args.role_arn)
    print(f"Injected execution role into {updated} functions in {args.template}")


if __name__ == "__main__":
    sys.exit(main())

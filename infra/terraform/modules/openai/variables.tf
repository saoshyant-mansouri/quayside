variable "prefix" {
  type = string
}

variable "suffix" {
  type = string
}

variable "location" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "chat_model_name" {
  type    = string
  default = "gpt-5-mini"
}

variable "chat_model_version" {
  type    = string
  default = "2025-08-07"
}

variable "embedding_model_name" {
  type    = string
  default = "text-embedding-3-small"
}

variable "embedding_model_version" {
  type    = string
  default = "1"
}

variable "deployment_capacity" {
  type    = number
  default = 100
}
